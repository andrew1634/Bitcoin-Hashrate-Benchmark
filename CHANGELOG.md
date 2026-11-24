# Changelog

Notable changes to Bitcoin Hashrate Benchmark are documented here.

## [1.0.2] - 2026-11-24

### Fixed

- Enabled cross-origin API access for the public leaderboard so the standalone browser page can load and submit results when opened as a local file.

## [1.0.1] - 2026-11-24

### Added

- Optional public leaderboard shared by the browser and Windows desktop apps.
- CPU/device model, architecture, source app, thread count, hashrate, and server submission date on leaderboard entries.
- Performance and newest-first sorting, with a crown for the fastest entry in each reported architecture category.
- Cloudflare Worker API with D1 storage, input validation, and approximate per-IP submission rate limiting.
- User confirmation before a result is published; public submissions are explicitly marked unverified.
- Cloudflare deployment and database migration instructions.

### Privacy

- Local run history stays on the device. Public submission is opt-in and shares the selected device and benchmark details.
- Browser hardware detection is best-effort; reported details can be edited before submitting.
- The leaderboard database does not store submitter IP addresses.

## [1.0.0] - 2026-11-24

### Added

- Windows desktop and standalone browser SHA-256 double-hash benchmarks.
- Windows x64 and ARM64 publishing support, with browser support for compatible ARM32 and ARM64 devices.
- Configurable test duration and worker/thread count, live hashrate, total hash count, elapsed time, and hashes per thread per second.
- Recent run history stored locally on the device.
