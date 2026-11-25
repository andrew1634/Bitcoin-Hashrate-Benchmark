# Bitcoin Hashrate Benchmark

**Browser and Windows desktop UNSTABLE beta v1.0.6**

> **UNSTABLE BETA WARNING:** Long-running tests can create sustained device load and heat; stop if your device gets hot or sluggish. Windows GPU benchmarking is experimental. All public scores are unverified.

> **Created by Andrew Miller** — building cross-platform apps and learning through hands-on projects.
>
> I hope this benchmark helps people compare device performance, learn about SHA-256, and explore how software works across platforms.
>
> **AI coding support:** Copilot Fusion

A local benchmark for comparing how devices perform SHA-256 double hashing. The Windows desktop app and standalone browser version are for learning and performance testing only. The Windows app can be published for x64 and ARM64; the browser version can run on compatible Windows, Android, and iOS browsers, including on ARM32 and ARM64 devices.

Version 1.0.6 is an unstable beta. Runs can continue until stopped manually. Use logical processors reported by the operating system or browser; Hyper-Threading is available only when the device and platform expose it.

## What this measures

Both versions repeatedly hash a synthetic 80-byte header with SHA-256 twice, similar to the hashing operation used in Bitcoin proof-of-work. The benchmark runs locally and never connects to the Bitcoin network or a mining pool, uses a wallet, or earns BTC. Sharing a result to the optional public leaderboard does send the selected benchmark and device details to its host. Real Bitcoin mining today requires specialized ASIC hardware.

## Run in a browser

Open [ChromebookHashrateBenchmark/index.html](./ChromebookHashrateBenchmark/index.html) in a recent browser that supports Web Crypto and Web Workers, such as Chrome on Windows or Android, or Safari on iOS. The browser benchmark is CPU-only, architecture-independent, and can run on ARM32 and ARM64 devices. It offers up to 64 workers based on the browser-reported logical processor count. Choose **Infinite** to run until you press **Stop**. The last 10 runs are saved locally in the browser; use **Clear history** to remove them. A page opened as a local file can use the public leaderboard too; it connects to the hosted Worker API over HTTPS. The page can still display CPU or Windows GPU leaderboard results.

## Optional public leaderboard

The public leaderboard has separate CPU and GPU rankings, sorts results by hashes per second, shows a crown for the fastest entry in each backend and architecture category, and includes the device model, architecture, source app, backend, thread count when applicable, submission date, and unverified status. Stopped runs up to seven days can be submitted. Submitting is always opt-in; the app asks before publishing a completed run. There are no accounts or display names. Browser hardware detection is best-effort: browsers often hide CPU/GPU model and architecture, so review or edit the detected values before submitting. All public entries are unverified and can be inaccurate. Cloudflare sees request IPs for its approximate rate limiter; this app does not save IP addresses in the leaderboard database.

The service uses Cloudflare Workers and D1. The documented Workers Free plan includes 100,000 Worker requests per day; D1 Free includes 5 million rows read and 100,000 rows written per day, with 5 GB of total storage. Exceeding daily quotas can temporarily stop leaderboard queries or submissions. Submissions are limited to five per IP per minute, but Cloudflare's rate limiter is approximate and scoped to individual locations. See the [Workers pricing and limits](https://developers.cloudflare.com/workers/platform/pricing/) and [D1 pricing](https://developers.cloudflare.com/d1/platform/pricing/).

The D1 database for this deployment has been created and is configured in `wrangler.jsonc`. To deploy from your local terminal, install a current Node.js version, sign in with `npx wrangler login`, then run these commands from the repository root:

```sh
npm install
npm run deploy
```

For Cloudflare Workers Builds, connect this repository's `main` branch to a Worker and set its deploy command to `npm run deploy`. Every deployment applies pending D1 migrations before publishing the Worker. The deployment serves the browser benchmark and API together. Open its `workers.dev` URL to use the online board. In the Windows app, paste that URL into **Service URL**; it is saved locally on that PC. Public leaderboard data is visible to anyone, and the API accepts only unverified submissions.

Run the API checks locally with `npm run test:leaderboard`.

## Changelog

See [CHANGELOG.md](./CHANGELOG.md) for release notes.

## Compare results carefully

The browser version uses Web Crypto and browser workers for CPU benchmarking only. The Windows CPU version uses .NET and the operating system's SHA-256 implementation; its optional GPU mode requires a hardware-accelerated Direct3D 12 adapter. CPU and GPU rankings are separate, and results from different backends are not directly comparable. Power mode, temperature, browser tabs, and other activity affect results. Use devices only with permission, and stop if they become too warm or sluggish.

## Run the desktop app on Windows

Install the .NET 10 SDK, then from this repository run:

```powershell
dotnet run --project .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj
```

The desktop app saves the last 10 runs to `%LOCALAPPDATA%\BitcoinHashrateBenchmark\history.json`.

To publish a self-contained, single-file Windows x64 app:

```powershell
dotnet publish .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

For native Windows ARM64:

```powershell
dotnet publish .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true
```

For a smaller download, publish a single-file app that uses the installed .NET 10 Desktop Runtime:

```powershell
dotnet publish .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

Use `win-arm64` in place of `win-x64` to publish the smaller ARM64 build. The self-contained apps are in `BitcoinHashrateBenchmark\bin\Release\net10.0-windows\<runtime>\publish`, where `<runtime>` is `win-x64` or `win-arm64`. Framework-dependent builds are much smaller but require the matching .NET 10 Desktop Runtime on the PC. The current .NET 10 Windows desktop project does not provide a native ARM32 target; use the browser version on ARM32 Windows devices with a compatible browser.
