using BuddyAI.Services;

namespace BuddyAI.Forms;

public sealed class UsageDashboardForm : Form
{
    public UsageDashboardForm(UsageMetricsService.UsageSummary summary)
    {
        Text = "BuddyAI Usage Dashboard";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 420);
        KeyPreview = true;

        Button cancelButton = new() { Visible = false };
        cancelButton.Click += (_, __) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancelButton);
        CancelButton = cancelButton;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        Controls.Add(layout);

        AddRow(layout, 0, "Today", Format(summary.Today));
        AddRow(layout, 1, "This Week", Format(summary.ThisWeek));
        AddRow(layout, 2, "This Month", Format(summary.ThisMonth));
        AddRow(layout, 3, "All Time", Format(summary.AllTime));
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, string value)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }, 0, row);
        layout.Controls.Add(new TextBox { Text = value, Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 10F) }, 1, row);
    }

    private static string Format(UsageMetricsService.UsageWindow window)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Requests: {window.Requests}",
            $"Prompt Tokens: {window.PromptTokens:N0}",
            $"Response Tokens: {window.ResponseTokens:N0}",
            $"Estimated Cost: ${window.EstimatedCostUsd:F4}",
            $"Average Latency: {window.AverageLatencyMs:F0} ms"
        });
    }
}
