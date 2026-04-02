namespace BuddyAI.Helpers;

/// <summary>
/// Encapsulates image loading, preview management, MIME type resolution,
/// and image-related state updates for the BuddyAI shell.
/// </summary>
internal static class ImageHelper
{
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    public static bool IsSupportedImageExtension(string? extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    }

    public static string GetMimeTypeFromExtension(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    /// <summary>
    /// Reads an image file from disk and returns the PNG-encoded bytes together with its MIME type.
    /// </summary>
    public static (byte[] Bytes, string MimeType) LoadImageBytes(string filePath)
    {
        string? extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (!IsSupportedImageExtension(extension))
            throw new InvalidOperationException("Unsupported image type. Use PNG, JPG, JPEG, BMP, GIF, or WEBP.");

        string mimeType = GetMimeTypeFromExtension(extension ?? ".png");
        using Bitmap bitmap = new(filePath);
        byte[] bytes = BitmapToPngBytes(bitmap);
        return (bytes, mimeType);
    }

    /// <summary>
    /// Converts a bitmap to PNG-encoded bytes.
    /// </summary>
    public static byte[] BitmapToPngBytes(Bitmap bitmap)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a clone of the provided image suitable for use as a PictureBox preview.
    /// The caller is responsible for disposing the returned image.
    /// </summary>
    public static Bitmap CloneForPreview(Image source)
    {
        return new Bitmap(source);
    }

    /// <summary>
    /// Builds a display string summarizing the loaded image.
    /// </summary>
    public static string FormatImageInfo(byte[]? imageBytes, string? imageMimeType, string? imagePath, bool modelSupportsImages)
    {
        if (imageBytes == null || string.IsNullOrWhiteSpace(imageMimeType))
        {
            return modelSupportsImages
                ? "No image selected."
                : "Image input is not supported by the selected model.";
        }

        string fileName = string.IsNullOrWhiteSpace(imagePath) ? "(captured image)" : Path.GetFileName(imagePath);
        double kb = imageBytes.Length / 1024d;
        string info = "Image: " + fileName + " | MIME: " + imageMimeType + " | Size: " + kb.ToString("F1") + " KB";

        if (!modelSupportsImages)
            info += " | Selected model does not support image input.";

        return info;
    }

    /// <summary>
    /// Builds a display string for a conversation tab's image panel.
    /// </summary>
    public static string FormatConversationImageInfo(string? imageName, string? imageMimeType, byte[]? imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            return "No image";

        double kb = imageBytes.Length / 1024d;
        return "Image: " + (imageName ?? "(captured image)") + " | MIME: " + (imageMimeType ?? "unknown") + " | Size: " + kb.ToString("F1") + " KB";
    }
}
