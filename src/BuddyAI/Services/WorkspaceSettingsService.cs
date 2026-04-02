using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class WorkspaceSettingsService
{
    private readonly string _path;

    public WorkspaceSettingsService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "workspace.settings.json");
    }

    public WorkspaceSettings Load()
    {
        if (!File.Exists(_path))
        {
            WorkspaceSettings settings = new();
            Save(settings);
            return settings;
        }

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<WorkspaceSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new WorkspaceSettings();
        }
        catch
        {
            return new WorkspaceSettings();
        }
    }

    public void Save(WorkspaceSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
