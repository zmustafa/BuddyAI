namespace BuddyAI.Services;

public sealed class DiagnosticsService
{
    private readonly List<string> _entries = new();

    public event EventHandler<string>? EntryAdded;

    public IReadOnlyList<string> Entries => _entries;

    public void Info(string message) => Add("INFO", message);
    public void Warn(string message) => Add("WARN", message);
    public void Error(string message) => Add("ERROR", message);

    public void Add(string level, string message)
    {
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}";
        _entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
    }

    public string GetFullText()
    {
        return string.Join(Environment.NewLine, _entries);
    }
}
