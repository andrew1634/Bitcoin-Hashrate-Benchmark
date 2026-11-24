using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace BitcoinHashrateBenchmark;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(15, 23, 42);
    private static readonly Color PanelColor = Color.FromArgb(30, 41, 59);
    private static readonly Color MutedColor = Color.FromArgb(148, 163, 184);
    private static readonly Color AccentColor = Color.FromArgb(34, 197, 94);

    private readonly ComboBox durationBox = new();
    private readonly NumericUpDown threadCountBox = new();
    private readonly Button startButton = new();
    private readonly Button clearHistoryButton = new();
    private readonly Button submitLeaderboardButton = new();
    private readonly Button refreshLeaderboardButton = new();
    private readonly TextBox leaderboardUrlBox = new();
    private readonly TextBox cpuModelBox = new();
    private readonly ComboBox architectureBox = new();
    private readonly Label statusLabel = new();
    private readonly Label historyStatusLabel = new();
    private readonly Label leaderboardStatusLabel = new();
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

    public MainForm()
    {
        Text = "Bitcoin Hashrate Benchmark v1.0.2";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 870);
        Size = new Size(760, 980);
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        BuildInterface();
        LoadHistory();
        LoadLeaderboardSettings();
        displayTimer.Tick += (_, _) => UpdateElapsed();
    }

    private void BuildInterface()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 28, 34, 24),
            ColumnCount = 1,
            RowCount = 8,
            BackColor = BackgroundColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(layout);

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        heading.Controls.Add(new Label
        {
            Text = "BITCOIN HASHRATE BENCHMARK",
            Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        });
        heading.Controls.Add(new Label
        {
            Text = "Measure your PC's SHA-256 double-hash speed | v1.0.2",
            Font = new Font("Segoe UI", 10),
            ForeColor = MutedColor,
            AutoSize = true,
            Location = new Point(2, 42)
        });
        heading.Controls.Add(new Label
        {
            Text = "Idea: Andrew Miller | AI coding: Copilot Fusion",
            Font = new Font("Segoe UI", 8),
            ForeColor = MutedColor,
            AutoSize = true,
            Location = new Point(2, 66)
        });
        layout.Controls.Add(heading, 0, 0);

        var settings = CreateCard();
        var settingsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 12),
            ColumnCount = 4,
            RowCount = 2,
            BackColor = PanelColor
        };
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        settings.Controls.Add(settingsGrid);

        settingsGrid.Controls.Add(CreateCaption("TEST DURATION"), 0, 0);
        settingsGrid.Controls.Add(CreateCaption("CPU THREADS"), 1, 0);

        durationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        durationBox.Items.AddRange(["5 seconds", "10 seconds", "30 seconds"]);
        durationBox.SelectedIndex = 1;
        StyleInput(durationBox);
        settingsGrid.Controls.Add(durationBox, 0, 1);

        threadCountBox.Minimum = 1;
        threadCountBox.Maximum = Math.Max(1, Environment.ProcessorCount);
        threadCountBox.Value = Math.Max(1, Environment.ProcessorCount);
        threadCountBox.ThousandsSeparator = true;
        StyleInput(threadCountBox);
        settingsGrid.Controls.Add(threadCountBox, 1, 1);
        layout.Controls.Add(settings, 0, 1);

        var results = CreateCard();
        var resultsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 12),
            ColumnCount = 2,
            RowCount = 2,
            BackColor = PanelColor
        };
        resultsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        resultsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        resultsLayout.Controls.Add(CreateMetric("HASH RATE", out rateLabel), 0, 0);
        resultsLayout.Controls.Add(CreateMetric("TOTAL HASHES", out hashesLabel), 1, 0);
        resultsLayout.Controls.Add(CreateMetric("ELAPSED", out elapsedLabel), 0, 1);
        results.Controls.Add(resultsLayout);
        layout.Controls.Add(results, 0, 2);

        var history = CreateCard();
        var historyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8),
            ColumnCount = 2,
            RowCount = 3,
            BackColor = PanelColor
        };
        historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        historyLayout.Controls.Add(CreateCaption("RECENT RUNS (LAST 10)"), 0, 0);

        clearHistoryButton.Text = "Clear";
        clearHistoryButton.Enabled = false;
        clearHistoryButton.Size = new Size(82, 25);
        clearHistoryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clearHistoryButton.BackColor = BackgroundColor;
        clearHistoryButton.ForeColor = Color.White;
        clearHistoryButton.FlatStyle = FlatStyle.Flat;
        clearHistoryButton.FlatAppearance.BorderColor = MutedColor;
        clearHistoryButton.Click += (_, _) => ClearHistory();
        historyLayout.Controls.Add(clearHistoryButton, 1, 0);

        historyList.Dock = DockStyle.Fill;
        historyList.BackColor = BackgroundColor;
        historyList.ForeColor = Color.White;
        historyList.BorderStyle = BorderStyle.None;
        historyList.HorizontalScrollbar = true;
        historyLayout.Controls.Add(historyList, 0, 1);
        historyLayout.SetColumnSpan(historyList, 2);

        historyStatusLabel.Text = "Saved locally on this PC.";
        historyStatusLabel.ForeColor = MutedColor;
        historyStatusLabel.AutoSize = true;
        historyLayout.Controls.Add(historyStatusLabel, 0, 2);
        historyLayout.SetColumnSpan(historyStatusLabel, 2);
        history.Controls.Add(historyLayout);
        layout.Controls.Add(history, 0, 3);

        var leaderboard = CreateCard();
        var leaderboardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = PanelColor
        };
        leaderboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leaderboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        leaderboardLayout.Controls.Add(CreateCaption("PUBLIC LEADERBOARD · UNVERIFIED"), 0, 0);

        var leaderboardSettings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = PanelColor
        };
        leaderboardSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        leaderboardSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        leaderboardSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        leaderboardSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        leaderboardSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leaderboardSettings.Controls.Add(CreateCaption("SERVICE URL"), 0, 0);
        leaderboardSettings.Controls.Add(CreateCaption("CPU MODEL (DETECTED, EDITABLE)"), 1, 0);
        leaderboardSettings.Controls.Add(CreateCaption("ARCH"), 2, 0);

        leaderboardUrlBox.PlaceholderText = "https://your-worker.workers.dev";
        leaderboardUrlBox.AccessibleName = "Public leaderboard service URL";
        StyleInput(leaderboardUrlBox);
        leaderboardSettings.Controls.Add(leaderboardUrlBox, 0, 1);

        cpuModelBox.Text = DetectCpuModel();
        cpuModelBox.AccessibleName = "CPU model; edit before submitting if incorrect";
        StyleInput(cpuModelBox);
        leaderboardSettings.Controls.Add(cpuModelBox, 1, 1);

        architectureBox.DropDownStyle = ComboBoxStyle.DropDownList;
        architectureBox.Items.AddRange(["ARM64", "ARM32", "x64", "x86", "Other", "Unknown"]);
        architectureBox.SelectedItem = DetectArchitecture();
        architectureBox.AccessibleName = "Processor architecture";
        StyleInput(architectureBox);
        leaderboardSettings.Controls.Add(architectureBox, 2, 1);
        leaderboardLayout.Controls.Add(leaderboardSettings, 0, 1);

        var leaderboardActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = PanelColor,
            Padding = new Padding(0)
        };
        submitLeaderboardButton.Text = "Submit latest run";
        submitLeaderboardButton.Enabled = false;
        submitLeaderboardButton.Width = 150;
        submitLeaderboardButton.Height = 28;
        submitLeaderboardButton.Click += async (_, _) => await SubmitLatestRunAsync();
        leaderboardActions.Controls.Add(submitLeaderboardButton);

        refreshLeaderboardButton.Text = "Refresh";
        refreshLeaderboardButton.Width = 90;
        refreshLeaderboardButton.Height = 28;
        refreshLeaderboardButton.Click += async (_, _) => await RefreshLeaderboardAsync();
        leaderboardActions.Controls.Add(refreshLeaderboardButton);
        leaderboardLayout.Controls.Add(leaderboardActions, 0, 2);

        leaderboardList.Dock = DockStyle.Fill;
        leaderboardList.BackColor = BackgroundColor;
        leaderboardList.ForeColor = Color.White;
        leaderboardList.BorderStyle = BorderStyle.None;
        leaderboardList.HorizontalScrollbar = true;
        leaderboardLayout.Controls.Add(leaderboardList, 0, 3);

        leaderboardStatusLabel.Text = "Submitting is optional; public entries include device details and are unverified.";
        leaderboardStatusLabel.ForeColor = MutedColor;
        leaderboardStatusLabel.AutoSize = true;
        leaderboardLayout.Controls.Add(leaderboardStatusLabel, 0, 4);
        leaderboard.Controls.Add(leaderboardLayout);
        layout.Controls.Add(leaderboard, 0, 4);

        progressBar.Dock = DockStyle.Fill;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.ForeColor = AccentColor;
        progressBar.Margin = new Padding(2, 12, 2, 12);
        layout.Controls.Add(progressBar, 0, 5);

        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        statusLabel.Text = "Ready when you are.";
        statusLabel.ForeColor = MutedColor;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(2, 12);
        bottom.Controls.Add(statusLabel);
        layout.Controls.Add(bottom, 0, 6);

        var actions = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        startButton.Text = "Start benchmark";
        startButton.BackColor = AccentColor;
        startButton.ForeColor = Color.FromArgb(5, 20, 12);
        startButton.FlatStyle = FlatStyle.Flat;
        startButton.FlatAppearance.BorderSize = 0;
        startButton.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        startButton.Size = new Size(180, 40);
        startButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        startButton.Location = new Point(actions.Width - startButton.Width, 4);
        startButton.Click += async (_, _) => await ToggleBenchmarkAsync();
        actions.Resize += (_, _) => startButton.Location = new Point(actions.ClientSize.Width - startButton.Width, 4);
        actions.Controls.Add(startButton);
        layout.Controls.Add(actions, 0, 7);
    }

    private async Task ToggleBenchmarkAsync()
    {
        if (runCancellation is not null)
        {
            runCancellation.Cancel();
            statusLabel.Text = "Stopping benchmark...";
            startButton.Enabled = false;
            return;
        }

        selectedSeconds = durationBox.SelectedIndex switch
        {
            0 => 5,
            2 => 30,
            _ => 10
        };
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
        statusLabel.Text = $"Running for {selectedSeconds} seconds...";
        SetInputsEnabled(false);

        var progress = new Progress<BenchmarkProgress>(UpdateProgress);

        try
        {
            var result = await HashRateBenchmark.RunAsync(
                TimeSpan.FromSeconds(selectedSeconds),
                (int)threadCountBox.Value,
                progress,
                runCancellation.Token);
            UpdateProgress(new BenchmarkProgress(result.Hashes, result.Elapsed));
            statusLabel.Text = runCancellation.IsCancellationRequested
                ? "Benchmark stopped."
                : "Benchmark complete.";
            SaveHistory(new BenchmarkHistoryEntry(
                DateTimeOffset.Now,
                (int)threadCountBox.Value,
                result.Hashes,
                result.Elapsed.TotalSeconds));
            if (!runCancellation.IsCancellationRequested)
            {
                latestResult = result;
                submitLeaderboardButton.Enabled = true;
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
            progressBar.Value = 100;
        }
    }

    private void UpdateProgress(BenchmarkProgress progress)
    {
        latestHashes = progress.Hashes;
        var rate = progress.Elapsed.TotalSeconds > 0 ? progress.Hashes / progress.Elapsed.TotalSeconds : 0;
        rateLabel.Text = FormatRate(rate);
        hashesLabel.Text = progress.Hashes.ToString("N0");
        elapsedLabel.Text = $"{progress.Elapsed.TotalSeconds:F1} s";
        progressBar.Value = Math.Clamp(
            (int)(progress.Elapsed.TotalSeconds / selectedSeconds * 100),
            0,
            100);
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

        progressBar.Value = Math.Clamp(
            (int)(runStopwatch.Elapsed.TotalSeconds / selectedSeconds * 100),
            0,
            100);
    }

    private void SetInputsEnabled(bool enabled)
    {
        durationBox.Enabled = enabled;
        threadCountBox.Enabled = enabled;
    }

    private void LoadHistory()
    {
        try
        {
            RenderHistory(historyStore.Load());
            historyStatusLabel.Text = "Saved locally on this PC.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
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
            var entries = await LeaderboardClient.GetAsync(leaderboardUrlBox.Text);
            leaderboardList.BeginUpdate();
            leaderboardList.Items.Clear();
            foreach (var entry in entries)
            {
                var crown = entry.CategoryRank == 1 ? "👑 " : "";
                leaderboardList.Items.Add(
                    $"{crown}{entry.HashesPerSecond:N0} H/s | {entry.Architecture} | {entry.CpuModel} | "
                    + $"{entry.SubmittedAt.LocalDateTime:g} | {entry.VerificationStatus}");
            }
            leaderboardList.EndUpdate();
            leaderboardStatusLabel.Text = entries.Count == 0
                ? "No public results yet."
                : $"Showing {entries.Count} results, sorted by hashes per second.";
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
            "This will publicly share your CPU model, architecture, hash count, speed, thread count, "
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
                    result.Hashes,
                    result.Elapsed.TotalSeconds,
                    (int)threadCountBox.Value));
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
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
            historyList.Items.Add(
                $"{entry.CompletedAt.LocalDateTime:g} | {entry.ElapsedSeconds:F1} s | "
                + $"{entry.ThreadCount} threads | {entry.Hashes:N0} hashes | {FormatRate(entry.HashesPerSecond)}");
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
            ForeColor = Color.White,
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
        control.BackColor = BackgroundColor;
        control.ForeColor = Color.White;
        control.Font = new Font("Segoe UI", 10);
    }
}
