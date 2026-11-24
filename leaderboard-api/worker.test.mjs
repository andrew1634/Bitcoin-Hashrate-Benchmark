import assert from "node:assert/strict";
import test from "node:test";
import worker from "./worker.mjs";

class TestDatabase {
  entries = [];
  query = "";
  boundValues = [];

  prepare(query) {
    this.query = query;
    return {
      bind: (...values) => ({
        all: async () => {
          this.boundValues = values;
          assert.match(query, /ROW_NUMBER\(\) OVER/);
          assert.match(query, /LIMIT \?/);
          const ordered = [...this.entries].sort((first, second) =>
            query.includes("submitted_at DESC, hashes_per_second DESC")
              ? second.submittedAt.localeCompare(first.submittedAt)
                || second.hashesPerSecond - first.hashesPerSecond
              : second.hashesPerSecond - first.hashesPerSecond
                || first.submittedAt.localeCompare(second.submittedAt));
          const categoryRanks = new Map();
          for (const entry of [...this.entries].sort((first, second) =>
            second.hashesPerSecond - first.hashesPerSecond
              || first.submittedAt.localeCompare(second.submittedAt))) {
            const rank = (categoryRanks.get(entry.architecture) ?? 0) + 1;
            categoryRanks.set(entry.architecture, rank);
            entry.categoryRank = rank;
          }
          return { results: ordered.slice(0, values[0]) };
        },
        run: async () => {
          const [
            id, cpuModel, architecture, source, hashes, elapsedSeconds,
            threadCount, hashesPerSecond, submittedAt
          ] = values;
          this.entries.push({
            id, cpuModel, architecture, source, hashes, elapsedSeconds,
            threadCount, hashesPerSecond, submittedAt, verificationStatus: "unverified"
          });
          return { success: true };
        }
      })
    };
  }
}

function environment(db = new TestDatabase()) {
  return {
    DB: db,
    SUBMISSION_LIMITER: { limit: async () => ({ success: true }) },
    ASSETS: { fetch: async () => new Response("benchmark page") }
  };
}

function makeRequest(path, method = "GET", body) {
  return new Request(`https://benchmark.example${path}`, {
    method,
    headers: body === undefined ? undefined : { "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
}

const validSubmission = {
  cpuModel: "Example CPU",
  architecture: "ARM64",
  source: "browser",
  hashes: 12_000,
  elapsedSeconds: 10,
  threadCount: 4
};

test("serves static assets for non-API requests", async () => {
  const response = await worker.fetch(makeRequest("/"), environment());
  assert.equal(await response.text(), "benchmark page");
});

test("allows cross-origin preflight requests from local browser files", async () => {
  const request = new Request("https://benchmark.example/api/submissions", {
    method: "OPTIONS",
    headers: {
      Origin: "null",
      "Access-Control-Request-Method": "POST",
      "Access-Control-Request-Headers": "content-type"
    }
  });
  const response = await worker.fetch(request, environment());
  assert.equal(response.status, 204);
  assert.equal(response.headers.get("access-control-allow-origin"), "*");
  assert.match(response.headers.get("access-control-allow-methods"), /POST/);
  assert.match(response.headers.get("access-control-allow-headers"), /Content-Type/i);
});

test("rejects malformed or out-of-range submissions", async () => {
  const db = new TestDatabase();
  for (const body of [
    { ...validSubmission, architecture: "riscv" },
    { ...validSubmission, hashes: -1 },
    { ...validSubmission, elapsedSeconds: 0.5 },
    { ...validSubmission, threadCount: 257 }
  ]) {
    const response = await worker.fetch(
      makeRequest("/api/submissions", "POST", body),
      environment(db)
    );
    assert.equal(response.status, 400);
  }
  assert.equal(db.entries.length, 0);
});

test("stores accepted entries as unverified and timestamps them on the server", async () => {
  const db = new TestDatabase();
  const response = await worker.fetch(
    makeRequest("/api/submissions", "POST", validSubmission),
    environment(db)
  );
  const result = await response.json();
  assert.equal(response.status, 201);
  assert.equal(result.verificationStatus, "unverified");
  assert.ok(Number.isFinite(Date.parse(result.submittedAt)));
  assert.equal(db.entries.length, 1);
  assert.equal(db.entries[0].hashesPerSecond, 1_200);
});

test("ranks category leaders while returning results sorted by performance", async () => {
  const db = new TestDatabase();
  for (const submission of [
    validSubmission,
    { ...validSubmission, hashes: 18_000 },
    { ...validSubmission, architecture: "x64", hashes: 2_000 }
  ]) {
    const response = await worker.fetch(
      makeRequest("/api/submissions", "POST", submission),
      environment(db)
    );
    assert.equal(response.status, 201);
  }

  const response = await worker.fetch(
    makeRequest("/api/leaderboard?sort=performance"),
    environment(db)
  );
  const { entries } = await response.json();
  assert.deepEqual(entries.map((entry) => entry.hashesPerSecond), [1_800, 1_200, 200]);
  assert.deepEqual(entries.map((entry) => entry.categoryRank), [1, 2, 1]);
  assert.ok(entries.every((entry) => entry.verificationStatus === "unverified"));
});

test("returns a bounded leaderboard with a supported sort order", async () => {
  const db = new TestDatabase();
  const response = await worker.fetch(
    makeRequest("/api/leaderboard?sort=newest&limit=1000"),
    environment(db)
  );
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("access-control-allow-origin"), "*");
  assert.deepEqual(await response.json(), { entries: [] });
  assert.match(db.query, /ORDER BY submitted_at DESC/);
  assert.equal(db.boundValues[0], 100);
});

test("caps oversized request bodies before parsing or writing", async () => {
  const db = new TestDatabase();
  const response = await worker.fetch(
    makeRequest("/api/submissions", "POST", { ...validSubmission, cpuModel: "x".repeat(5000) }),
    environment(db)
  );
  assert.equal(response.status, 413);
  assert.equal(db.entries.length, 0);
});

test("rate limits public submissions before writing to the database", async () => {
  const db = new TestDatabase();
  const env = environment(db);
  env.SUBMISSION_LIMITER.limit = async () => ({ success: false });
  const response = await worker.fetch(
    makeRequest("/api/submissions", "POST", validSubmission),
    env
  );
  assert.equal(response.status, 429);
  assert.equal(db.entries.length, 0);
});
