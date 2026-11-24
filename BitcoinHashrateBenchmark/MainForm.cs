using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

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
    private readonly Label statusLabel = new();
    private readonly Label historyStatusLabel = new();
    private readonly ListBox historyList = new();
    private Label rateLabel = new();
    private Label hashesLabel = new();
    private Label elapsedLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly System.Windows.Forms.Timer displayTimer = new() { Interval = 250 };
    private readonly BenchmarkHistoryStore historyStore = new();
    private CancellationTokenSource? runCancellation;
    private Stopwatch? runStopwatch;
    private long latestHashes;
    private int selectedSeconds;

    public MainForm()
    {
        Text = "Bitcoin Hashrate Benchmark v1.0.0";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 700);
        Size = new Size(760, 780);
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        BuildInterface();
        LoadHistory();
        displayTimer.Tick += (_, _) => UpdateElapsed();
    }

    private void BuildInterface()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(34, 28, 34, 24),
            ColumnCount = 1,
            RowCount = 7,
            BackColor = BackgroundColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
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
            Text = "Measure your PC's SHA-256 double-hash speed | v1.0.0",
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

        progressBar.Dock = DockStyle.Fill;
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.ForeColor = AccentColor;
        progressBar.Margin = new Padding(2, 12, 2, 12);
        layout.Controls.Add(progressBar, 0, 4);

        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        statusLabel.Text = "Ready when you are.";
        statusLabel.ForeColor = MutedColor;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(2, 12);
        bottom.Controls.Add(statusLabel);
        layout.Controls.Add(bottom, 0, 5);

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
        layout.Controls.Add(actions, 0, 6);
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
