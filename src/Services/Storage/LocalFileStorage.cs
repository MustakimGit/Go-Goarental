using System.Text.RegularExpressions;

namespace GoGoaRentals.Services.Storage;

/// <summary>Stores files on local disk under a root folder outside the web root. Swap for blob storage in production.</summary>
public sealed partial class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken cancellationToken = default)
    {
        if (!SafeSegment().IsMatch(folder) || !SafeSegment().IsMatch(extension))
        {
            throw new ArgumentException("Folder and extension must be simple names.");
        }

        var key = $"{folder}/{Guid.NewGuid():N}.{extension}";
        var path = ResolvePath(key)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Stream? OpenRead(string key)
    {
        var path = ResolvePath(key);
        return path is not null && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string key)
    {
        var path = ResolvePath(key);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    // Keys are always "<folder>/<guid>.<ext>"; anything else (e.g. "..") is rejected.
    private string? ResolvePath(string key)
    {
        var match = KeyPattern().Match(key);
        return match.Success ? Path.Combine(rootPath, match.Groups[1].Value, match.Groups[2].Value) : null;
    }

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex SafeSegment();

    [GeneratedRegex("^([a-z0-9-]{1,40})/([0-9a-f]{32}\\.[a-z0-9]{1,10})$")]
    private static partial Regex KeyPattern();
}
