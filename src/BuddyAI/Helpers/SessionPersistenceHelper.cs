using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI.Helpers;

/// <summary>
/// Manages serializing/deserializing the full application session state
/// (conversations, composer state, prompt history) and workspace layout settings.
/// Extracted from AIQ to keep persistence logic isolated.
/// </summary>
internal sealed class SessionPersistenceHelper
{
    private readonly SessionStateService _sessionStateService;
    private readonly WorkspaceSettingsService _workspaceSettingsService;
    private readonly DiagnosticsService _diagnostics;

    public SessionPersistenceHelper(
        SessionStateService sessionStateService,
        WorkspaceSettingsService workspaceSettingsService,
        DiagnosticsService diagnostics)
    {
        _sessionStateService = sessionStateService;
        _workspaceSettingsService = workspaceSettingsService;
        _diagnostics = diagnostics;
    }

    public AppSessionState LoadSession() => _sessionStateService.Load();

    public void SaveSession(AppSessionState session)
    {
        _sessionStateService.Save(session);
        _diagnostics.Info("Session saved to disk.");
    }

    public bool TrySaveSession(AppSessionState session)
    {
        try
        {
            SaveSession(session);
            return true;
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Session save failed: " + ex.Message);
            return false;
        }
    }

    public void ClearSession() => _sessionStateService.Clear();

    public WorkspaceSettings LoadWorkspace() => _workspaceSettingsService.Load();

    public void SaveWorkspace(WorkspaceSettings settings)
    {
        _workspaceSettingsService.Save(settings);
    }

    public bool TrySaveWorkspace(WorkspaceSettings settings)
    {
        try
        {
            SaveWorkspace(settings);
            return true;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Workspace settings save skipped: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Restores the composer-level image state from the saved session.
    /// Returns the restored image bytes, MIME type, path, and preview image.
    /// The caller owns the returned preview image and must dispose it.
    /// </summary>
    public (byte[]? ImageBytes, string? MimeType, string? Path, Image? Preview) RestoreComposerImage(AppSessionState session)
    {
        if (string.IsNullOrWhiteSpace(session.CurrentImageBase64))
            return (null, null, null, null);

        try
        {
            byte[] imageBytes = Convert.FromBase64String(session.CurrentImageBase64);
            string mimeType = string.IsNullOrWhiteSpace(session.CurrentImageMimeType) ? "image/png" : session.CurrentImageMimeType!;
            string? path = session.CurrentImagePath;
            using MemoryStream ms = new(imageBytes);
            using Image temp = Image.FromStream(ms);
            Bitmap preview = new(temp);
            return (imageBytes, mimeType, path, preview);
        }
        catch
        {
            return (null, null, null, null);
        }
    }
}
