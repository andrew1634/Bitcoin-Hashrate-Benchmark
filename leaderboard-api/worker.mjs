const architectures = new Set(["ARM64", "ARM32", "x64", "x86", "Other", "Unknown"]);
const sources = new Set(["browser", "windows"]);
const benchmarkTypes = new Set(["cpu", "gpu"]);
const backendsByType = {
  cpu: new Set(["unknown", "browser-webcrypto", "windows-sha256"]),
  gpu: new Set(["webgpu", "webgl2", "direct3d12"])
};
const maxEntries = 100;
const maxModelLength = 100;
const maxElapsedSeconds = 604_800;
const corsHeaders = {
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET, POST, OPTIONS",
  "access-control-allow-headers": "Accept, Content-Type",
  "access-control-max-age": "86400"
};

function respond(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff",
      ...corsHeaders
    }
  });
}

function error(message, status) {
  return respond({ error: message }, status);
}

function normalizeSubmission(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return { error: "The submission must be a JSON object." };
  }

  const {
    cpuModel, architecture, source, hashes, elapsedSeconds, threadCount
  } = value;
  const benchmarkType = value.benchmarkType ?? "cpu";
  const backend = value.backend ?? "unknown";
  if (typeof cpuModel !== "string" || cpuModel.trim().length > maxModelLength) {
    return { error: `Device model must be a string no longer than ${maxModelLength} characters.` };
  }
  if ([...cpuModel].some((character) => character.charCodeAt(0) < 32)) {
    return { error: "Device model cannot contain control characters." };
  }
  if (!architectures.has(architecture)) {
    return { error: "Architecture is not supported." };
  }
  if (!sources.has(source)) {
    return { error: "Submission source is not supported." };
  }
  if (!benchmarkTypes.has(benchmarkType)) {
    return { error: "Benchmark type is not supported." };
  }
  if (!backendsByType[benchmarkType].has(backend)) {
    return { error: "Benchmark backend is not supported for this benchmark type." };
  }
  if (!Number.isSafeInteger(hashes) || hashes < 1) {
    return { error: "Hashes must be a positive whole number." };
  }
  if (!Number.isFinite(elapsedSeconds) || elapsedSeconds < 1 || elapsedSeconds > maxElapsedSeconds) {
    return { error: `Elapsed time must be between 1 and ${maxElapsedSeconds} seconds.` };
  }
  if (!Number.isSafeInteger(threadCount) || threadCount < 1 || threadCount > 256) {
    return { error: "Thread count must be between 1 and 256." };
  }

  const hashesPerSecond = hashes / elapsedSeconds;
  if (!Number.isFinite(hashesPerSecond) || hashesPerSecond > 1_000_000_000) {
    return { error: "The calculated hashrate is outside the accepted range." };
  }

  return {
    submission: {
      cpuModel: cpuModel.trim() || "Unreported",
      architecture,
      source,
      benchmarkType,
      backend,
      hashes,
      elapsedSeconds,
      threadCount,
      hashesPerSecond
    }
  };
}

async function readLimitedBody(request, maxBytes) {
  const reader = request.body?.getReader();
  if (!reader) return "";

  const chunks = [];
  let totalBytes = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    totalBytes += value.byteLength;
    if (totalBytes > maxBytes) {
      await reader.cancel();
      return null;
    }
    chunks.push(value);
  }

  const body = new Uint8Array(totalBytes);
  let offset = 0;
  for (const chunk of chunks) {
    body.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return new TextDecoder("utf-8", { fatal: true }).decode(body);
}

function hashesMatch(first, second) {
  if (first.byteLength !== second.byteLength) return false;
  let difference = 0;
  for (let index = 0; index < first.byteLength; index++) {
    difference |= first[index] ^ second[index];
  }
  return difference === 0;
}

async function verifyDeveloperUnlock(request, env) {
  const expectedCode = env.DEVELOPER_UNLOCK_CODE;
  if (typeof expectedCode !== "string" || expectedCode.length < 12 || expectedCode.length > 256) {
    return error("Developer unlock is not configured.", 503);
  }

  const clientIp = request.headers.get("CF-Connecting-IP") ?? "unknown";
  const { success } = await env.SUBMISSION_LIMITER.limit({ key: `developer-unlock:${clientIp}` });
  if (!success) return error("Too many code attempts. Please wait a minute and try again.", 429);

  let body;
  try {
    body = await readLimitedBody(request, 1024);
  } catch {
    return error("Request body must be valid UTF-8.", 400);
  }
  if (body === null) return error("Request body is too large.", 413);

  let value;
  try {
    value = JSON.parse(body);
  } catch {
    return error("Request body must be valid JSON.", 400);
  }
  if (!value || typeof value !== "object" || Array.isArray(value)
    || typeof value.code !== "string" || value.code.length < 1 || value.code.length > 256) {
    return error("A valid developer code is required.", 400);
  }

  const encoder = new TextEncoder();
  const [expectedDigest, suppliedDigest] = await Promise.all([
    crypto.subtle.digest("SHA-256", encoder.encode(expectedCode)),
    crypto.subtle.digest("SHA-256", encoder.encode(value.code))
  ]);
  if (!hashesMatch(new Uint8Array(expectedDigest), new Uint8Array(suppliedDigest))) {
    return error("The developer code was not accepted.", 401);
  }

  return respond({ unlocked: true });
}

async function getLeaderboard(request, db) {
  const url = new URL(request.url);
  const sort = url.searchParams.get("sort") === "newest" ? "newest" : "performance";
  const benchmarkType = url.searchParams.get("type") ?? "cpu";
  if (!benchmarkTypes.has(benchmarkType)) {
    return error("Benchmark type must be cpu or gpu.", 400);
  }
  const requestedLimit = Number(url.searchParams.get("limit") ?? maxEntries);
  const limit = Number.isSafeInteger(requestedLimit)
    ? Math.max(1, Math.min(requestedLimit, maxEntries))
    : maxEntries;
  const orderBy = sort === "newest"
    ? "submitted_at DESC, hashes_per_second DESC, id ASC"
    : "hashes_per_second DESC, submitted_at ASC, id ASC";
  const statement = db.prepare(`
    SELECT id, cpu_model AS cpuModel, architecture, source,
      benchmark_type AS benchmarkType, backend, hashes,
      elapsed_seconds AS elapsedSeconds, thread_count AS threadCount,
      hashes_per_second AS hashesPerSecond, submitted_at AS submittedAt,
      verification_status AS verificationStatus,
      ROW_NUMBER() OVER (
        PARTITION BY benchmark_type, backend, architecture
        ORDER BY hashes_per_second DESC, submitted_at ASC, id ASC
      ) AS categoryRank
    FROM leaderboard_entries
    WHERE benchmark_type = ?
    ORDER BY ${orderBy}
    LIMIT ?
  `).bind(benchmarkType, limit);
  const result = await statement.all();
  return respond({ entries: result.results ?? [] });
}

async function submit(request, env) {
  const clientIp = request.headers.get("CF-Connecting-IP") ?? "unknown";
  const { success } = await env.SUBMISSION_LIMITER.limit({ key: clientIp });
  if (!success) return error("Too many submissions. Please wait a minute and try again.", 429);

  let body;
  try {
    body = await readLimitedBody(request, 4096);
  } catch {
    return error("Request body must be valid UTF-8.", 400);
  }
  if (body === null) return error("Submission is too large.", 413);

  let value;
  try {
    value = JSON.parse(body);
  } catch {
    return error("Request body must be valid JSON.", 400);
  }

  const normalized = normalizeSubmission(value);
  if (normalized.error) return error(normalized.error, 400);

  const entry = normalized.submission;
  const id = crypto.randomUUID();
  const submittedAt = new Date().toISOString();
  await env.DB.prepare(`
    INSERT INTO leaderboard_entries (
      id, cpu_model, architecture, source, benchmark_type, backend, hashes,
      elapsed_seconds, thread_count, hashes_per_second, submitted_at, verification_status
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'unverified')
  `).bind(
    id,
    entry.cpuModel,
    entry.architecture,
    entry.source,
    entry.benchmarkType,
    entry.backend,
    entry.hashes,
    entry.elapsedSeconds,
    entry.threadCount,
    entry.hashesPerSecond,
    submittedAt
  ).run();

  return respond({ id, submittedAt, verificationStatus: "unverified" }, 201);
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (!url.pathname.startsWith("/api/")) {
      return env.ASSETS.fetch(request);
    }

    if (request.method === "OPTIONS"
      && (url.pathname === "/api/leaderboard"
        || url.pathname === "/api/submissions"
        || url.pathname === "/api/developer-unlock")) {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    if (url.pathname === "/api/leaderboard" && request.method === "GET") {
      try {
        return await getLeaderboard(request, env.DB);
      } catch (cause) {
        console.error("Could not read the public leaderboard.", cause);
        return error("The leaderboard is temporarily unavailable.", 503);
      }
    }

    if (url.pathname === "/api/submissions" && request.method === "POST") {
      try {
        return await submit(request, env);
      } catch (cause) {
        console.error("Could not save a public leaderboard submission.", cause);
        return error("The submission could not be saved. Please try again later.", 503);
      }
    }

    if (url.pathname === "/api/developer-unlock" && request.method === "POST") {
      try {
        return await verifyDeveloperUnlock(request, env);
      } catch (cause) {
        console.error("Could not validate a developer code.", cause);
        return error("Developer code validation is temporarily unavailable.", 503);
      }
    }

    if (url.pathname.startsWith("/api/")) {
      return error("API endpoint not found.", 404);
    }

    return error("Method not allowed.", 405);
  }
};
