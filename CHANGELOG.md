# Changelog

Notable changes to Bitcoin Hashrate Benchmark are documented here.

## [1.0.9] - Unstable Beta

### Browser updates

- Fixed developer settings being visible before code verification; the title now opens a hidden developer-settings dialog.
- Simplified the browser layout and moved appearance/custom options behind the server-validated, session-only unlock.
- Added developer-gated browser memory bandwidth and randomized latency tests with separate bounded working-set settings.
- Matched standard browser durations to Windows and kept browser memory history local-only.
- Made browser memory bandwidth runs responsive to Stop, including infinite-duration runs.

## [1.0.8] - Unstable Beta

### Added

- CSV export and compatible recent-run score comparisons in the browser and Windows app.
- Configurable Windows RAM benchmark working sets from 8 to 256 MiB.
- Server-validated developer settings unlock, with per-client attempt rate limiting.

### Changed

- Developer unlock is no longer embedded in the v1.0.8 browser or Windows app; Cloudflare must have the `DEVELOPER_UNLOCK_CODE` secret configured.
- The unlock is session-only and is not trusted from saved preferences.
- Updated browser and Windows version labels to unstable beta v1.0.8.

### Notes

- The previous v1.0.7 client-side code was already public and cannot be made secret retroactively.
- The developer code is not payment verification.

## [1.0.7] - Unstable Beta

### Added

- Windows-only timed RAM read/write bandwidth and randomized pointer-chase latency benchmarks.
- Light/dark theme selection in the browser and Windows desktop app.
- Developer-code-gated customization for accent color, font size, custom test duration, and CPU worker count.

### Notes

- Memory benchmark results are stored locally and are not submitted to the CPU/GPU leaderboard.
- The developer code is checked client-side and does not verify payment.
- The Windows memory test uses two 64 MiB buffers for bandwidth and a 64 MiB pointer chain plus a 64 MiB setup array for latency.

## [1.0.6] - Unstable Beta

### Added

- Infinite-duration CPU and Windows GPU runs that continue until stopped manually.
- CPU worker counts use OS/browser-reported logical processors, including Hyper-Threading where available.
- Browser leaderboard can still display both CPU and Windows GPU results.

### Changed

- Removed GPU hashing from the HTML app; GPU benchmarking remains available in the Windows app.
- Public submissions now accept stopped runs up to seven days.

## [1.0.5] - Unstable Beta

### Added

- Optional browser GPU benchmarking using WebGPU with WebGL2 fallback.
- Optional Windows Direct3D 12 GPU benchmarking on hardware-accelerated supported adapters.
- Separate CPU and GPU public leaderboard rankings, including backend labels.
- Persistent unstable-beta notice and GPU heat/performance warning in both apps.

### Notes

- GPU results remain unverified and are not directly comparable across different backends.
- WebGPU and Direct3D 12 require supported devices and drivers; unsupported hardware reports an error instead of silently switching to CPU.
- This release is experimental; GPU mode has not been validated on every supported device.

## [1.0.4] - Beta

### Changed

- Updated the Windows app to a plain white interface with standard Windows controls.
- Updated browser and Windows version labels for the 1.0.4 beta.
- Windows downloads are distributed as standalone executables rather than ZIP archives.

## [1.0.3] - Beta

### Fixed

- Replaced the cramped Windows desktop layout with separate benchmark, recent-runs, and leaderboard tabs.
- Set the Windows leaderboard service to the live HTTPS endpoint by default, avoiding the blank-URL prompt on first launch.

### Changed

- Marked the browser and Windows desktop builds as beta releases.
- GPU benchmarking is not included in this beta.

## [1.0.2] - 2026-11-24

Browser/HTML release only. The Windows desktop app remains at version 1.0.1.

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
