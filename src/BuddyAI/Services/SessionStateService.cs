using System.Text;
using System.Text.Json;
using BuddyAI.Models;

namespace BuddyAI.Services;

public sealed class SessionStateService
{
    private readonly string _folderPath;
    private readonly string _jsonPath;

    public SessionStateService()
    {
        // Reuse the existing storage root so the solution remains deployment-friendly.
        _folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BuddyAIDesktop");

        _jsonPath = Path.Combine(_folderPath, "session.json");
    }

    public string GetStoragePath() => _jsonPath;

    public AppSessionState Load()
    {
        if (!File.Exists(_jsonPath))
            return new AppSessionState();

        try
        {
            string json = File.ReadAllText(_jsonPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSessionState>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppSessionState();
        }
        catch
        {
            return new AppSessionState();
        }
    }

    public void Save(AppSessionState session)
    {
        Directory.CreateDirectory(_folderPath);
        string json = JsonSerializer.Serialize(session, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_jsonPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Clear()
    {
        if (File.Exists(_jsonPath))
            File.Delete(_jsonPath);
    }
}
