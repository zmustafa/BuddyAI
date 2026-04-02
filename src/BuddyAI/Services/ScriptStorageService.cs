namespace BuddyAI.Services;

public sealed class ScriptStorageService
{
    private readonly string _root;
    private readonly string _scriptBaseName;
    private readonly string _extension;
    private readonly string _generationFolder;
    private int _iteration = 0;

    public ScriptStorageService(string root, string cloudPrefix, string scriptName, string extension)
    {
        _root = root;
        Directory.CreateDirectory(_root);

        _scriptBaseName = SanitizeFileName(scriptName);
        _extension = extension.StartsWith(".") ? extension : "." + extension;

        var prefix = string.IsNullOrWhiteSpace(cloudPrefix) ? "job" : cloudPrefix.Trim().ToLowerInvariant();
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _generationFolder = Path.Combine(_root, $"{prefix}_{_scriptBaseName}_{stamp}");
        Directory.CreateDirectory(_generationFolder);
    }

    public string GenerationFolder => _generationFolder;

    public string SaveNext(string content)
    {
        _iteration++;
        var version = _iteration.ToString("D2");
        var fileName = $"{_scriptBaseName}.{version}{_extension}";
        var path = Path.Combine(_generationFolder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "script";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Trim();
    }
}