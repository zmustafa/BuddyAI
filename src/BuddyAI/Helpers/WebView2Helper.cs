using Microsoft.Web.WebView2.Core;

namespace BuddyAI.Helpers;

internal static class WebView2Helper
{
    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BuddyAI", "WebView2");

    public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: UserDataFolder);
    }
}
