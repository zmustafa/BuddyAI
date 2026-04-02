using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class UsageMetricsService
{
    private readonly string _path;
    private readonly List<UsageRecord> _records = new();

    public UsageMetricsService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "usage.metrics.json");
        Load();
    }

    public IReadOnlyList<UsageRecord> Records => _records;

    public UsageRecord Track(string model, string provider, string prompt, string response, double latencyMs)
    {
        int promptTokens = EstimateTokens(prompt);
        int responseTokens = EstimateTokens(response);
        decimal cost = EstimateCostUsd(model, promptTokens, responseTokens);

        UsageRecord record = new()
        {
            TimestampUtc = DateTime.UtcNow,
            Model = model ?? string.Empty,
            Provider = provider ?? string.Empty,
            PromptCharacters = prompt?.Length ?? 0,
            ResponseCharacters = response?.Length ?? 0,
            EstimatedPromptTokens = promptTokens,
            EstimatedResponseTokens = responseTokens,
            EstimatedCostUsd = cost,
            LatencyMs = latencyMs
        };

        _records.Add(record);
        Save();
        return record;
    }

    public UsageSummary GetSummary()
    {
        DateTime now = DateTime.UtcNow;
        DateTime today = now.Date;
        DateTime weekStart = today.AddDays(-(int)today.DayOfWeek);
        DateTime monthStart = new DateTime(now.Year, now.Month, 1);

        return new UsageSummary
        {
            Today = BuildWindow(_records.Where(x => x.TimestampUtc >= today)),
            ThisWeek = BuildWindow(_records.Where(x => x.TimestampUtc >= weekStart)),
            ThisMonth = BuildWindow(_records.Where(x => x.TimestampUtc >= monthStart)),
            AllTime = BuildWindow(_records)
        };
    }

    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    public static decimal EstimateCostUsd(string? model, int promptTokens, int responseTokens)
    {
        string m = (model ?? string.Empty).ToLowerInvariant();

        decimal inputPer1k;
        decimal outputPer1k;

        if (m.Contains("gpt-5.4"))
        {
            inputPer1k = 0.015m;
            outputPer1k = 0.030m;
        }
        else if (m.Contains("gpt-5.3") || m.Contains("codex"))
        {
            inputPer1k = 0.010m;
            outputPer1k = 0.020m;
        }
        else if (m.Contains("gpt-4.1-mini"))
        {
            inputPer1k = 0.001m;
            outputPer1k = 0.002m;
        }
        else
        {
            inputPer1k = 0.004m;
            outputPer1k = 0.008m;
        }

        return ((promptTokens / 1000m) * inputPer1k) + ((responseTokens / 1000m) * outputPer1k);
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        try
        {
            string json = File.ReadAllText(_path);
            List<UsageRecord>? loaded = JsonSerializer.Deserialize<List<UsageRecord>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loaded != null)
                _records.AddRange(loaded);
        }
        catch
        {
        }
    }

    private void Save()
    {
        string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    public void Purge()
    {
        _records.Clear();
        Save();
    }

    private static UsageWindow BuildWindow(IEnumerable<UsageRecord> records)
    {
        UsageRecord[] array = records.ToArray();
        return new UsageWindow
        {
            Requests = array.Length,
            PromptTokens = array.Sum(x => x.EstimatedPromptTokens),
            ResponseTokens = array.Sum(x => x.EstimatedResponseTokens),
            EstimatedCostUsd = array.Sum(x => x.EstimatedCostUsd),
            AverageLatencyMs = array.Length == 0 ? 0d : array.Average(x => x.LatencyMs)
        };
    }

    public sealed class UsageSummary
    {
        public UsageWindow Today { get; set; } = new();
        public UsageWindow ThisWeek { get; set; } = new();
        public UsageWindow ThisMonth { get; set; } = new();
        public UsageWindow AllTime { get; set; } = new();
    }

    public sealed class UsageWindow
    {
        public int Requests { get; set; }
        public int PromptTokens { get; set; }
        public int ResponseTokens { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public double AverageLatencyMs { get; set; }
    }
}
