using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace BitcoinHashrateBenchmark;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.White;
    private static readonly Color PanelColor = Color.White;
    private static readonly Color MutedColor = Color.FromArgb(96, 96, 96);
    private static readonly Color AccentColor = SystemColors.Highlight;

    private readonly ComboBox durationBox = new();
    private readonly ComboBox benchmarkModeBox = new();
    private readonly NumericUpDown threadCountBox = new();
    private readonly Button startButton = new();
    private readonly Button clearHistoryButton = new();
    private readonly Button submitLeaderboardButton = new();
    private readonly Button refreshLeaderboardButton = new();
    private readonly TextBox leaderboardUrlBox = new();
    private readonly TextBox cpuModelBox = new();
    private readonly ComboBox architectureBox = new();
    private readonly ComboBox leaderboardTypeBox = new();
    private readonly Label statusLabel = new();
    private readonly Label historyStatusLabel = new();
    private readonly Label leaderboardStatusLabel = new();
    private readonly Label deviceModelCaption = new();
    private readonly Label threadCountCaption = new();
    private readonly ListBox historyList = new();
    private readonly ListBox leaderboardList = new();
    private Label rateLabel = new();
    private Label hashesLabel = new();
    private Label elapsedLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly System.Windows.Forms.Timer displayTimer = new() { Interval = 250 };
    private readonly BenchmarkHistoryStore historyStore = new();
    private readonly LeaderboardSettingsStore leaderboardSettingsStore = new();
    private CancellationTokenSource? runCancellation;
    private Stopwatch? runStopwatch;
    private BenchmarkResult? latestResult;
    private long latestHashes;
    private int selectedSeconds;
    private int latestThreadCount;
    private bool infiniteRun;
    private bool stoppedByUser;
    private string latestBenchmarkType = "cpu";
    private string latestBackend = "windows-sha256";

    public MainForm()
    {
        Text = "Bitcoin Hashrate Benchmark v1.0.6 UNSTABLE";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 560);
        Size = new Size(860, 680);
        BackColor = BackgroundColor;
        ForeColor = Color.Black;
        Font = new Font("Segoe UI", 10);

        BuildInterface();
        LoadHistory();
        LoadLeaderboardSettings();
        displayTimer.Tick += (_, _) => UpdateElapsed();
    }

    private void BuildInterface()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 6),
            Font = new Font("Segoe UI", 10)
        };
        Controls.Add(tabs);

        var benchmarkPage = new TabPage("Benchmark") { BackColor = BackgroundColor };
        var benchmarkLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = BackgroundColor
        };
        benchmarkLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        benchmarkLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        benchmarkPage.Controls.Add(benchmarkLayout);

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        heading.Controls.Add(new Label
        {
            Text = "Bitcoin Hashrate Benchmark",
            Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold),
            ForeColor = Color.Black,
            AutoSize = true,
            Location = new Point(0, 0)
        });
        heading.Controls.Add(new Label
        {
            Text = "Local CPU/GPU SHA-256 benchmark · Version 1.0.6 UNSTABLE",
            Font = new Font("Segoe UI", 10),
            ForeColor = MutedColor,
            AutoSize = true,
            Location = new Point(2, 36)
        });
        heading.Controls.Add(new Label
        {
            Text = "UNSTABLE BETA: GPU results may be inaccurate; stop if your device gets hot or sluggish.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(150, 70, 0),
            AutoSize = true,
            Location = new Point(2, 61)
        });
        benchmarkLayout.Controls.Add(heading, 0, 0);

        var settings = CreateCard();
        var settingsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 8),
            ColumnCount = 3,
            RowCount = 2,
            BackColor = PanelColor
        };
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        settings.Controls.Add(settingsGrid);
        settingsGrid.Controls.Add(CreateCaption("BENCHMARK DEVICE"), 0, 0);
        settingsGrid.Controls.Add(CreateCaption("TEST DURATION"), 1, 0);
        threadCountCaption.Text = "CPU LOGICAL THREADS";
        threadCountCaption.Dock = DockStyle.Fill;
        threadCountCaption.TextAlign = ContentAlignment.BottomLeft;
        threadCountCaption.ForeColor = MutedColor;
        settingsGrid.Controls.Add(threadCountCaption, 2, 0);
        benchmarkModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        benchmarkModeBox.Items.AddRange(["CPU", "GPU (Direct3D 12)"]);
        benchmarkModeBox.SelectedIndex = 0;
        StyleInput(benchmarkModeBox);
        benchmarkModeBox.SelectedIndexChanged += (_, _) => UpdateBenchmarkMode();
        settingsGrid.Controls.Add(benchmarkModeBox, 0, 1);
        durationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        durationBox.Items.AddRange(["5 seconds", "10 seconds", "30 seconds", "Infinite"]);
        durationBox.SelectedIndex = 1;
        StyleInput(durationBox);
        settingsGrid.Controls.Add(durationBox, 1, 1);
        threadCountBox.Minimum = 1;
        threadCountBox.Maximum = Math.Max(1, Environment.ProcessorCount);
        threadCountBox.Value = Math.Max(1, Environment.ProcessorCount);
        threadCountBox.ThousandsSeparator = true;
        StyleInput(threadCountBox);
        settingsGrid.Controls.Add(threadCountBox, 2, 1);
        benchmarkLayout.Controls.Add(settings, 0, 1);

        var results = CreateCard();
        var resultsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            ColumnCount = 3,
            RowCount = 1,
            BackColor = PanelColor
        };
        for (var index = 0; index < 3; index++)
        {
            resultsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        }
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultsLayout.Controls.Add(CreateMetric("HASH RATE", out rateLabel), 0, 0);
        resultsLayout.Controls.Add(CreateMetric("TOTAL HASHES", out hashesLabel), 1, 0);
        resultsLayout.Controls.Add(CreateMetric("ELAPSED", out elapsedLabel), 2, 0);
        results.Controls.Add(resultsLayout);
        benchmarkLayout.Controls.Add(results, 0, 2);

        progressBar.Dock = DockStyle.Fill;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.ForeColor = AccentColor;
        progressBar.Margin = new Padding(2, 10, 2, 10);
        benchmarkLayout.Controls.Add(progressBar, 0, 3);

        statusLabel.Text = "Ready when you are.";
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.ForeColor = MutedColor;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        benchmarkLayout.Controls.Add(statusLabel, 0, 4);

        var benchmarkActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = BackgroundColor
        };
        startButton.Text = "Start benchmark";
        startButton.UseVisualStyleBackColor = true;
        startButton.FlatStyle = FlatStyle.Standard;
        startButton.Font = new Font("Segoe UI", 10);
        startButton.Size = new Size(160, 34);
        startButton.Click += async (_, _) => await ToggleBenchmarkAsync();
        benchmarkActions.Controls.Add(startButton);
        benchmarkLayout.Controls.Add(benchmarkActions, 0, 5);

        var historyPage = new TabPage("Recent runs") { BackColor = BackgroundColor };
        var historyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 2,
            RowCount = 3,
            BackColor = BackgroundColor
        };
        historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        historyPage.Controls.Add(historyLayout);
        historyLayout.Controls.Add(new Label
        {
            Text = "Recent runs (last 10)",
            Dock = DockStyle.Fill,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold)
        }, 0, 0);
        clearHistoryButton.Text = "Clear";
        clearHistoryButton.Enabled = false;
        clearHistoryButton.Dock = DockStyle.Fill;
        clearHistoryButton.UseVisualStyleBackColor = true;
        clearHistoryButton.FlatStyle = FlatStyle.Standard;
        clearHistoryButton.Click += (_, _) => ClearHistory();
        historyLayout.Controls.Add(clearHistoryButton, 1, 0);
        historyList.Dock = DockStyle.Fill;
        historyList.BackColor = PanelColor;
        historyList.ForeColor = Color.Black;
        historyList.BorderStyle = BorderStyle.FixedSingle;
        historyList.HorizontalScrollbar = true;
        historyLayout.Controls.Add(historyList, 0, 1);
        historyLayout.SetColumnSpan(historyList, 2);
        historyStatusLabel.Text = "Saved locally on this PC.";
        historyStatusLabel.ForeColor = MutedColor;
        historyStatusLabel.Dock = DockStyle.Fill;
        historyStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        historyLayout.Controls.Add(historyStatusLabel, 0, 2);
        historyLayout.SetColumnSpan(historyStatusLabel, 2);
        tabs.TabPages.Add(benchmarkPage);
        tabs.TabPages.Add(historyPage);

        var leaderboardPage = new TabPage("Leaderboard") { BackColor = BackgroundColor };
        var leaderboardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 6,
            BackColor = BackgroundColor
        };
        leaderboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        leaderboardPage.Controls.Add(leaderboardLayout);
        leaderboardLayout.Controls.Add(new Label
        {
            Text = "Public leaderboard · submissions are unverified",
            Dock = DockStyle.Fill,
            ForeColor = Color.Black,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold)
        }, 0, 0);

        var leaderboardSettings = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 96,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = PanelColor
        };
        leaderboardSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leaderboardSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leaderboardSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leaderboardSettings.Padding = new Padding(12);
        leaderboardSettings.Controls.Add(CreateCaption("SERVICE URL"), 0, 0);
        leaderboardUrlBox.PlaceholderText = LeaderboardSettingsStore.DefaultServiceUrl;
        leaderboardUrlBox.AccessibleName = "Public leaderboard service URL";
        StyleInput(leaderboardUrlBox);
        leaderboardSettings.Controls.Add(leaderboardUrlBox, 0, 1);
        leaderboardLayout.Controls.Add(leaderboardSettings, 0, 1);

        var deviceDetails = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = BackgroundColor
        };
        deviceDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        deviceDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        deviceDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        deviceDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        deviceDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        deviceModelCaption.Text = "CPU MODEL (EDITABLE)";
        deviceModelCaption.Dock = DockStyle.Fill;
        deviceModelCaption.TextAlign = ContentAlignment.BottomLeft;
        deviceModelCaption.ForeColor = MutedColor;
        deviceDetails.Controls.Add(deviceModelCaption, 0, 0);
        deviceDetails.Controls.Add(CreateCaption("ARCHITECTURE"), 2, 0);
        cpuModelBox.Text = DetectCpuModel();
        cpuModelBox.AccessibleName = "CPU model; edit before submitting if incorrect";
        StyleInput(cpuModelBox);
        deviceDetails.Controls.Add(cpuModelBox, 0, 1);
        architectureBox.DropDownStyle = ComboBoxStyle.DropDownList;
        architectureBox.Items.AddRange(["ARM64", "ARM32", "x64", "x86", "Other", "Unknown"]);
        architectureBox.SelectedItem = DetectArchitecture();
        architectureBox.AccessibleName = "Processor architecture";
        StyleInput(architectureBox);
        deviceDetails.Controls.Add(architectureBox, 2, 1);
        leaderboardLayout.Controls.Add(deviceDetails, 0, 2);

        var leaderboardActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = BackgroundColor,
            Padding = new Padding(0, 4, 0, 0)
        };
        submitLeaderboardButton.Text = "Submit latest run";
        submitLeaderboardButton.Enabled = false;
        submitLeaderboardButton.Width = 150;
        submitLeaderboardButton.Height = 36;
        submitLeaderboardButton.Click += async (_, _) => await SubmitLatestRunAsync();
        leaderboardActions.Controls.Add(submitLeaderboardButton);
        leaderboardTypeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        leaderboardTypeBox.Items.AddRange(["CPU", "GPU"]);
        leaderboardTypeBox.SelectedIndex = 0;
        leaderboardTypeBox.Width = 80;
        leaderboardTypeBox.Height = 36;
        leaderboardTypeBox.SelectedIndexChanged += async (_, _) => await RefreshLeaderboardAsync();
        leaderboardActions.Controls.Add(leaderboardTypeBox);
        refreshLeaderboardButton.Text = "Refresh results";
        refreshLeaderboardButton.Width = 120;
        refreshLeaderboardButton.Height = 36;
        refreshLeaderboardButton.Click += async (_, _) => await RefreshLeaderboardAsync();
        leaderboardActions.Controls.Add(refreshLeaderboardButton);
        leaderboardLayout.Controls.Add(leaderboardActions, 0, 3);
        leaderboardList.Dock = DockStyle.Fill;
        leaderboardList.BackColor = PanelColor;
        leaderboardList.ForeColor = Color.Black;
        leaderboardList.BorderStyle = BorderStyle.FixedSingle;
        leaderboardList.HorizontalScrollbar = true;
        leaderboardLayout.Controls.Add(leaderboardList, 0, 4);
        leaderboardStatusLabel.Text = "Submitting is optional. Public entries are unverified.";
        leaderboardStatusLabel.ForeColor = MutedColor;
        leaderboardStatusLabel.Dock = DockStyle.Fill;
        leaderboardStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        leaderboardLayout.Controls.Add(leaderboardStatusLabel, 0, 5);
        tabs.TabPages.Add(leaderboardPage);
    }

    private async Task ToggleBenchmarkAsync()
    {
        if (runCancellation is not null)
        {
            stoppedByUser = true;
            runCancellation.Cancel();
            statusLabel.Text = "Stopping benchmark...";
            startButton.Enabled = false;
            return;
        }

        infiniteRun = durationBox.SelectedIndex == 3;
        selectedSeconds = infiniteRun ? 0 : durationBox.SelectedIndex switch
        {
            0 => 5,
            2 => 30,
            _ => 10
        };
        stoppedByUser = false;
        var previousBenchmarkType = latestBenchmarkType;
        var hadLatestResult = latestResult.HasValue;
        var useGpu = benchmarkModeBox.SelectedIndex == 1;
        var nextBenchmarkType = useGpu ? "gpu" : "cpu";
        if (hadLatestResult && previousBenchmarkType != nextBenchmarkType)
        {
            deviceModelCaption.Text = useGpu ? "GPU MODEL (EDITABLE)" : "CPU MODEL (EDITABLE)";
            cpuModelBox.Text = useGpu ? "Unreported GPU" : DetectCpuModel();
        }
        latestBenchmarkType = nextBenchmarkType;
        latestBackend = useGpu ? GpuHashRateBenchmark.Backend : "windows-sha256";
        latestThreadCount = useGpu ? 1 : (int)threadCountBox.Value;
        latestResult = null;
        submitLeaderboardButton.Enabled = false;
        latestHashes = 0;
        rateLabel.Text = "0 H/s";
        hashesLabel.Text = "0";
        elapsedLabel.Text = "0.0 s";
        progressBar.Value = 0;

        runCancellation = new CancellationTokenSource();
        runStopwatch = Stopwatch.StartNew();
        displayTimer.Start();
        startButton.Text = "Stop benchmark";
        statusLabel.Text = infiniteRun
            ? $"Running {latestBenchmarkType.ToUpperInvariant()} benchmark until you stop it..."
            : $"Running {latestBenchmarkType.ToUpperInvariant()} benchmark for {selectedSeconds} seconds...";
        SetInputsEnabled(false);
        progressBar.Style = infiniteRun ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;

        var progress = new Progress<BenchmarkProgress>(UpdateProgress);

        try
        {
            var duration = infiniteRun ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(selectedSeconds);
            var result = useGpu
                ? await GpuHashRateBenchmark.RunAsync(duration, progress, runCancellation.Token)
                : await HashRateBenchmark.RunAsync(
                    duration,
                    latestThreadCount,
                    progress,
                    runCancellation.Token);
            UpdateProgress(new BenchmarkProgress(result.Hashes, result.Elapsed));
            statusLabel.Text = stoppedByUser
                ? "Benchmark stopped and saved."
                : "Benchmark complete.";
            SaveHistory(new BenchmarkHistoryEntry(
                DateTimeOffset.Now,
                latestThreadCount,
                result.Hashes,
                result.Elapsed.TotalSeconds)
            {
                BenchmarkType = latestBenchmarkType,
                Backend = latestBackend
            });
            if (!stoppedByUser || infiniteRun)
            {
                latestResult = result;
                var elapsedSeconds = result.Elapsed.TotalSeconds;
                submitLeaderboardButton.Enabled = elapsedSeconds is >= 1 and <= 604_800;
                if (!submitLeaderboardButton.Enabled)
                {
                    statusLabel.Text = "Run saved locally; public submissions support runs from 1 second to 7 days.";
                }
            }
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"Benchmark failed: {exception.Message}";
        }
        finally
        {
            displayTimer.Stop();
            runStopwatch?.Stop();
            runStopwatch = null;
            runCancellation.Dispose();
            runCancellation = null;
            SetInputsEnabled(true);
            startButton.Enabled = true;
            startButton.Text = "Start benchmark";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = latestResult.HasValue ? 100 : 0;
        }
    }

    private void UpdateProgress(BenchmarkProgress progress)
    {
        latestHashes = progress.Hashes;
        var rate = progress.Elapsed.TotalSeconds > 0 ? progress.Hashes / progress.Elapsed.TotalSeconds : 0;
        rateLabel.Text = FormatRate(rate);
        hashesLabel.Text = progress.Hashes.ToString("N0");
        elapsedLabel.Text = $"{progress.Elapsed.TotalSeconds:F1} s";
        if (!infiniteRun)
        {
            progressBar.Value = Math.Clamp(
                (int)(progress.Elapsed.TotalSeconds / selectedSeconds * 100),
                0,
                100);
        }
    }

    private void UpdateElapsed()
    {
        if (runStopwatch is null)
        {
            return;
        }

        elapsedLabel.Text = $"{runStopwatch.Elapsed.TotalSeconds:F1} s";
        if (runStopwatch.Elapsed.TotalSeconds > 0)
        {
            rateLabel.Text = FormatRate(latestHashes / runStopwatch.Elapsed.TotalSeconds);
        }

        if (!infiniteRun)
        {
            progressBar.Value = Math.Clamp(
                (int)(runStopwatch.Elapsed.TotalSeconds / selectedSeconds * 100),
                0,
                100);
        }
    }

    private void SetInputsEnabled(bool enabled)
    {
        benchmarkModeBox.Enabled = enabled;
        durationBox.Enabled = enabled;
        threadCountBox.Enabled = enabled && benchmarkModeBox.SelectedIndex == 0;
    }

    private void UpdateBenchmarkMode()
    {
        var gpuSelected = benchmarkModeBox.SelectedIndex == 1;
        threadCountBox.Enabled = !gpuSelected && runCancellation is null;
        threadCountCaption.Text = gpuSelected ? "GPU DISPATCH" : "CPU LOGICAL THREADS";
        if (latestResult.HasValue)
        {
            statusLabel.Text = $"Device details describe the latest {latestBenchmarkType.ToUpperInvariant()} run.";
            return;
        }

        deviceModelCaption.Text = gpuSelected ? "GPU MODEL (EDITABLE)" : "CPU MODEL (EDITABLE)";
        cpuModelBox.Text = gpuSelected ? "Unreported GPU" : DetectCpuModel();
        if (runCancellation is null)
        {
            statusLabel.Text = gpuSelected
                ? "Requires a Direct3D 12 GPU. Stop if your device gets hot or sluggish."
                : "Ready when you are.";
        }
    }

    private void LoadHistory()
    {
        try
        {
            RenderHistory(historyStore.Load());
            historyStatusLabel.Text = "Saved locally on this PC.";
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            clearHistoryButton.Enabled = true;
            historyStatusLabel.Text = $"Could not load history; use Clear to remove it: {exception.Message}";
        }
    }

    private void LoadLeaderboardSettings()
    {
        try
        {
            leaderboardUrlBox.Text = leaderboardSettingsStore.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            leaderboardStatusLabel.Text = $"Could not load leaderboard settings: {exception.Message}";
        }
    }

    private async Task RefreshLeaderboardAsync()
    {
        try
        {
            SaveLeaderboardUrl();
            refreshLeaderboardButton.Enabled = false;
            leaderboardStatusLabel.Text = "Loading public results...";
            var benchmarkType = leaderboardTypeBox.SelectedItem?.ToString()?.ToLowerInvariant() ?? "cpu";
            var entries = await LeaderboardClient.GetAsync(leaderboardUrlBox.Text, benchmarkType);
            leaderboardList.BeginUpdate();
            leaderboardList.Items.Clear();
            foreach (var entry in entries)
            {
                var crown = entry.CategoryRank == 1 ? "👑 " : "";
                leaderboardList.Items.Add(
                    $"{crown}{entry.HashesPerSecond:N0} H/s | {entry.BenchmarkType.ToUpperInvariant()} · {entry.Backend} | "
                    + $"{entry.Architecture} | {entry.CpuModel} | {entry.SubmittedAt.LocalDateTime:g} | "
                    + $"{entry.VerificationStatus}");
            }
            leaderboardList.EndUpdate();
            leaderboardStatusLabel.Text = entries.Count == 0
                ? "No public results yet."
                : $"Showing {entries.Count} {benchmarkType.ToUpperInvariant()} results, sorted by hashes per second.";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or UriFormatException or JsonException or TaskCanceledException or
                InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            leaderboardStatusLabel.Text = $"Could not load leaderboard: {exception.Message}";
        }
        finally
        {
            refreshLeaderboardButton.Enabled = true;
        }
    }

    private async Task SubmitLatestRunAsync()
    {
        if (latestResult is not { } result)
        {
            leaderboardStatusLabel.Text = "Complete a benchmark before submitting a result.";
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"This will publicly share your {(latestBenchmarkType == "gpu" ? "GPU" : "CPU")} model, architecture, "
            + "benchmark type, backend, hash count, speed, thread count, "
            + "and submission date. The result will be marked unverified. Continue?",
            "Submit public result",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (confirmation != DialogResult.Yes) return;

        try
        {
            SaveLeaderboardUrl();
            submitLeaderboardButton.Enabled = false;
            leaderboardStatusLabel.Text = "Submitting result...";
            await LeaderboardClient.SubmitAsync(
                leaderboardUrlBox.Text,
                new LeaderboardSubmission(
                    string.IsNullOrWhiteSpace(cpuModelBox.Text) ? "Unreported" : cpuModelBox.Text.Trim(),
                    architectureBox.SelectedItem?.ToString() ?? "Unknown",
                    "windows",
                    latestBenchmarkType,
                    latestBackend,
                    result.Hashes,
                    result.Elapsed.TotalSeconds,
                    latestThreadCount));
            leaderboardStatusLabel.Text = "Result submitted as unverified.";
            await RefreshLeaderboardAsync();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or UriFormatException or JsonException or TaskCanceledException or
                InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            leaderboardStatusLabel.Text = $"Could not submit result: {exception.Message}";
        }
        finally
        {
            submitLeaderboardButton.Enabled = latestResult.HasValue;
        }
    }

    private void SaveLeaderboardUrl()
    {
        leaderboardSettingsStore.Save(leaderboardUrlBox.Text);
    }

    private static string DetectCpuModel()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                null) as string
                ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
                ?? "Unreported";
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unreported";
        }
    }

    private static string DetectArchitecture() =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "ARM64",
            Architecture.Arm => "ARM32",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => "Unknown"
        };

    private void SaveHistory(BenchmarkHistoryEntry entry)
    {
        try
        {
            RenderHistory(historyStore.Add(entry));
            historyStatusLabel.Text = "Run saved locally.";
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            historyStatusLabel.Text = $"Could not save history: {exception.Message}";
        }
    }

    private void ClearHistory()
    {
        try
        {
            historyStore.Clear();
            RenderHistory([]);
            historyStatusLabel.Text = "Run history cleared.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            historyStatusLabel.Text = $"Could not clear history: {exception.Message}";
        }
    }

    private void RenderHistory(IEnumerable<BenchmarkHistoryEntry> entries)
    {
        historyList.BeginUpdate();
        historyList.Items.Clear();
        foreach (var entry in entries)
        {
            var benchmarkType = entry.BenchmarkType.ToUpperInvariant();
            var benchmarkDetails = entry.BenchmarkType == "cpu"
                ? $"{entry.ThreadCount} threads"
                : entry.Backend;
            historyList.Items.Add(
                $"{entry.CompletedAt.LocalDateTime:g} | {benchmarkType} · {entry.Backend} | "
                + $"{entry.ElapsedSeconds:F1} s | {benchmarkDetails} | {entry.Hashes:N0} hashes | "
                + $"{FormatRate(entry.HashesPerSecond)}");
        }
        historyList.EndUpdate();
        clearHistoryButton.Enabled = historyList.Items.Count > 0;
    }

    private static string FormatRate(double hashesPerSecond) =>
        hashesPerSecond >= 1_000_000
            ? $"{hashesPerSecond / 1_000_000:F2} MH/s"
            : hashesPerSecond >= 1_000
                ? $"{hashesPerSecond / 1_000:F2} kH/s"
                : $"{hashesPerSecond:F0} H/s";

    private static Panel CreateCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = PanelColor,
        Padding = new Padding(0),
        Margin = new Padding(0, 0, 0, 12)
    };

    private static Label CreateCaption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.BottomLeft,
        ForeColor = MutedColor,
        Font = new Font("Segoe UI Semibold", 8, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 4)
    };

    private static Panel CreateMetric(string title, out Label value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor };
        panel.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI Semibold", 8, FontStyle.Bold),
            Location = new Point(0, 1)
        });
        value = new Label
        {
            Text = "—",
            AutoSize = true,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI Semibold", 16, FontStyle.Bold),
            Location = new Point(0, 23)
        };
        panel.Controls.Add(value);
        return panel;
    }

    private static void StyleInput(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0);
        control.BackColor = Color.White;
        control.ForeColor = Color.Black;
        control.Font = new Font("Segoe UI", 10);
    }
}
