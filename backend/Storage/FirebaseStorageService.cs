using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Backend.Storage;

public sealed class FirebaseStorageService
{
    private static readonly Dictionary<string, string> MimeNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "image/jpeg",
        ["image/jpg"] = "image/jpeg",
        ["image/pjpeg"] = "image/jpeg",
        ["image/png"] = "image/png",
        ["image/x-png"] = "image/png",
        ["image/gif"] = "image/gif",
        ["image/webp"] = "image/webp",
        ["application/octet-stream"] = "image/webp",
        ["image/heic"] = "image/heic",
        ["image/heif"] = "image/heif",
        ["application/heic"] = "image/heic",
        ["application/heif"] = "image/heif"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".jfif",
        ".png",
        ".gif",
        ".webp",
        ".heic",
        ".heif"
    };

    private static readonly Dictionary<string, string> ExtensionMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".jfif"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB ceiling for uploads
    private const string ItemPrefix = "item_images";
    private const string ProfilePrefix = "profile_images";

    private readonly StorageClient _storageClient;
    private readonly StorageOptions _options;

    public FirebaseStorageService(StorageClient storageClient, IOptions<StorageOptions> options)
    {
        _storageClient = storageClient ?? throw new ArgumentNullException(nameof(storageClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            throw new InvalidOperationException("Storage bucket name must be configured.");
        }
    }

    public Task<UploadResult> UploadItemImageAsync(IFormFile file, string? ownerId, CancellationToken cancellationToken = default)
        => UploadAsync(file, ownerId, ItemPrefix, cancellationToken);

    public Task<UploadResult> UploadProfileImageAsync(IFormFile file, string? ownerId, CancellationToken cancellationToken = default)
        => UploadAsync(file, ownerId, ProfilePrefix, cancellationToken);

    private async Task<UploadResult> UploadAsync(IFormFile file, string? ownerId, string prefix, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("Image file is required.", nameof(file));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Image exceeds the 5 MB upload limit.");
        }

        var extension = NormalizeExtension(Path.GetExtension(file.FileName));
        var contentType = NormalizeContentType(file.ContentType, extension);

        var objectName = BuildObjectName(ownerId, extension, prefix);

        var uploadOptions = new UploadObjectOptions
        {
            PredefinedAcl = PredefinedObjectAcl.PublicRead
        };

        await using var stream = file.OpenReadStream();
        var storageObject = await _storageClient.UploadObjectAsync(
            _options.Bucket,
            objectName,
            contentType,
            stream,
            uploadOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Default public URL pattern for Google Cloud Storage
        var url = $"https://storage.googleapis.com/{_options.Bucket}/{Uri.EscapeDataString(storageObject.Name)}";
        return new UploadResult(storageObject.Name, url);
    }

    private static string BuildObjectName(string? ownerId, string extension, string prefix)
    {
        var safeOwner = string.IsNullOrWhiteSpace(ownerId)
            ? "anonymous"
            : ownerId.Trim();

        var guid = Guid.NewGuid().ToString("N");

        var trimmedPrefix = string.IsNullOrWhiteSpace(prefix) ? "uploads" : prefix.TrimEnd('/');
        return $"{trimmedPrefix}/{safeOwner}/{guid}{extension}";
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".jpg";
        }

        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        return AllowedExtensions.Contains(extension)
            ? extension.ToLowerInvariant()
            : ".jpg";
    }

    private static string NormalizeContentType(string? contentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            MimeNormalization.TryGetValue(contentType, out var normalized))
        {
            return normalized;
        }

        if (ExtensionMimeMap.TryGetValue(extension, out var mapped))
        {
            return mapped;
        }

        return "image/jpeg";
    }
}

public sealed record UploadResult(string ObjectName, string Url);

public sealed class StorageOptions
{
    public string Bucket { get; set; } = string.Empty;
}
