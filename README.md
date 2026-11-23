# Bitcoin Hashrate Benchmark

A local benchmark for comparing how devices perform SHA-256 double hashing. The Windows desktop app and standalone browser version are for learning and performance testing only. The Windows app can be published for x64 and ARM64; the browser version can run on compatible Windows, Android, and iOS browsers, including on ARM32 and ARM64 devices.

## What this measures

Both versions repeatedly hash a synthetic 80-byte header with SHA-256 twice, similar to the hashing operation used in Bitcoin proof-of-work. They run locally: neither connects to the Bitcoin network or a mining pool, uses a wallet, nor earns BTC. Real Bitcoin mining today requires specialized ASIC hardware.

## Run in a browser

Open [ChromebookHashrateBenchmark/index.html](./ChromebookHashrateBenchmark/index.html) in a recent browser that supports Web Crypto and Web Workers, such as Chrome on Windows or Android, or Safari on iOS. The benchmark is architecture-independent and can run on ARM32 and ARM64 devices. Choose a duration and number of browser workers; the default uses the available logical processors (up to 16). The page makes no network requests. Some mobile browsers restrict locally opened files; if so, open it from a secure HTTPS site instead.

## Compare results carefully

The browser version uses the browser's Web Crypto API and browser workers, while the Windows version uses .NET and the operating system's SHA-256 implementation. Their results are not directly comparable. Power mode, temperature, browser tabs, and other activity affect results. Use devices only with permission, and stop if they become too warm or sluggish.

## Run the desktop app on Windows

Install the .NET 10 SDK, then from this repository run:

```powershell
dotnet run --project .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj
```

To publish a self-contained, single-file Windows x64 app:

```powershell
dotnet publish .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

For native Windows ARM64:

```powershell
dotnet publish .\BitcoinHashrateBenchmark\BitcoinHashrateBenchmark.csproj -c Release -r win-arm64 --self-contained true /p:PublishSingleFile=true
```

The published app is in `BitcoinHashrateBenchmark\bin\Release\net10.0-windows\<runtime>\publish`, where `<runtime>` is `win-x64` or `win-arm64`. The current .NET 10 Windows desktop project does not provide a native ARM32 target; use the browser version on ARM32 Windows devices with a compatible browser.
