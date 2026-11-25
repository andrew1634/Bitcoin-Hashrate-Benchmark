ALTER TABLE leaderboard_entries
    ADD COLUMN benchmark_type TEXT NOT NULL DEFAULT 'cpu'
        CHECK (benchmark_type IN ('cpu', 'gpu'));

ALTER TABLE leaderboard_entries
    ADD COLUMN backend TEXT NOT NULL DEFAULT 'unknown'
        CHECK (backend IN (
            'unknown',
            'browser-webcrypto',
            'windows-sha256',
            'webgpu',
            'webgl2',
            'direct3d12'
        ));

CREATE INDEX leaderboard_by_type_performance
    ON leaderboard_entries (benchmark_type, hashes_per_second DESC, submitted_at ASC, id ASC);

CREATE INDEX leaderboard_by_type_architecture
    ON leaderboard_entries (benchmark_type, backend, architecture, hashes_per_second DESC, submitted_at ASC, id ASC);
