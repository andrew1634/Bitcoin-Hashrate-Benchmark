CREATE TABLE leaderboard_entries (
    id TEXT PRIMARY KEY NOT NULL,
    cpu_model TEXT NOT NULL,
    architecture TEXT NOT NULL CHECK (
        architecture IN ('ARM64', 'ARM32', 'x64', 'x86', 'Other', 'Unknown')
    ),
    source TEXT NOT NULL CHECK (source IN ('browser', 'windows')),
    hashes INTEGER NOT NULL CHECK (hashes > 0),
    elapsed_seconds REAL NOT NULL CHECK (elapsed_seconds > 0),
    thread_count INTEGER NOT NULL CHECK (thread_count BETWEEN 1 AND 256),
    hashes_per_second REAL NOT NULL CHECK (hashes_per_second > 0),
    submitted_at TEXT NOT NULL,
    verification_status TEXT NOT NULL DEFAULT 'unverified'
        CHECK (verification_status IN ('verified', 'unverified'))
);

CREATE INDEX leaderboard_by_performance
    ON leaderboard_entries (hashes_per_second DESC, submitted_at ASC, id ASC);

CREATE INDEX leaderboard_by_architecture
    ON leaderboard_entries (architecture, hashes_per_second DESC, submitted_at ASC, id ASC);
