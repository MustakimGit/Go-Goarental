using GoGoaRentals.Services.Common;

namespace GoGoaRentals.Services.Fleet;

/// <summary>Which uploads are accepted as vehicle documents, checked by content rather than trusting the file name.</summary>
internal static class DocumentFileRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly (string Extension, string ContentType, byte[] Signature)[] Formats =
    [
        ("pdf", "application/pdf", "%PDF"u8.ToArray()),
        ("png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ("jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]),
    ];

    /// <summary>Reads the upload into memory and returns it with the extension that matches its content.</summary>
    public static async Task<(MemoryStream Content, string Extension)> ReadAndValidateAsync(Stream upload, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await CopyWithLimitAsync(upload, buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new BusinessRuleException("The file is empty.");
        }

        var extension = DetectExtension(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            ?? throw new BusinessRuleException("Upload a PDF, JPG or PNG file.");
        buffer.Position = 0;
        return (buffer, extension);
    }

    private static string? DetectExtension(ReadOnlySpan<byte> bytes)
    {
        foreach (var (extension, _, signature) in Formats)
        {
            if (bytes.StartsWith(signature))
            {
                return extension;
            }
        }

        return null;
    }

    public static string ContentTypeFor(string key)
    {
        var extension = Path.GetExtension(key).TrimStart('.');
        return Formats.FirstOrDefault(f => f.Extension == extension).ContentType ?? "application/octet-stream";
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (destination.Length + read > MaxBytes)
            {
                throw new BusinessRuleException("Files must be 5 MB or smaller.");
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
