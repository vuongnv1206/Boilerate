using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Domain.Common;

namespace Boilerate.Infrastructure.FileStorage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private readonly string _root;

    public LocalFileStorageService()
        : this(Path.GetFullPath("Files"))
    {
    }

    internal LocalFileStorageService(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public Task<string> UploadAsync<T>(Microsoft.AspNetCore.Http.IFormFile? file, FileType supportedFileType,
        CancellationToken cancellationToken = default)
        where T : class => UploadCoreAsync(file, supportedFileType, typeof(T).Name, null, cancellationToken);

    internal Task<string> UploadCommerceAsync(Microsoft.AspNetCore.Http.IFormFile file, string storageKey, CancellationToken ct) =>
        UploadCoreAsync(file, FileType.Image, "Commerce", storageKey, ct);

    private async Task<string> UploadCoreAsync(Microsoft.AspNetCore.Http.IFormFile? file, FileType supportedFileType,
        string folder, string? storageKey, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return string.Empty;
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (supportedFileType != FileType.Image || extension is not (".png" or ".jpg" or ".jpeg") || file.Length > MaximumImageBytes)
        {
            throw new FluentValidation.ValidationException("Only JPEG/PNG images up to 5 MiB are supported.");
        }

        await using Stream input = file.OpenReadStream();
        byte[] signature = new byte[8];
        int read = await input.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, cancellationToken);
        bool png = read == 8 && signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = read >= 3 && signature[0] == 255 && signature[1] == 216 && signature[2] == 255;
        if ((extension == ".png" && !png) || (extension != ".png" && !jpeg))
        {
            throw new FluentValidation.ValidationException("Image content does not match its extension.");
        }

        string relative = storageKey ?? $"Files/Images/{folder}/{Guid.NewGuid():N}{extension}";
        string target = Resolve(relative, allowAbsolute: false);
        EnsureNoLinks(target, allowMissing: true);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        EnsureNoLinks(target, allowMissing: false, skipLeaf: true);
        bool created = false;
        try
        {
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await output.WriteAsync(signature.AsMemory(0, read), cancellationToken);
            byte[] buffer = new byte[81920];
            long total = read;
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
            {
                total += count;
                if (total > MaximumImageBytes)
                {
                    throw new FluentValidation.ValidationException("Image exceeds 5 MiB.");
                }

                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
        }
        catch
        {
            // The stream has been disposed before removing an incomplete upload.
            if (created)
            {
                File.Delete(target);
            }

            throw;
        }

        return relative;
    }

    public Stream OpenRead(string path)
    {
        string target = Resolve(path, allowAbsolute: false);
        EnsureNoLinks(target, allowMissing: false);
        return new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    }

    public void Remove(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            return;
        }

        string target = Resolve(path, allowAbsolute: true);
        EnsureNoLinks(target, allowMissing: true);
        try { File.Delete(target); }
        catch (DirectoryNotFoundException) { /* Missing storage object is already removed. */ }
    }

    private string Resolve(string path, bool allowAbsolute)
    {
        string target;
        if (allowAbsolute && Path.IsPathFullyQualified(path))
        {
            target = Path.GetFullPath(path);
        }
        else
        {
            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Files/", StringComparison.Ordinal) || normalized.Contains(':') || normalized.Contains('\0'))
            {
                throw new ForbiddenException("Invalid storage path.");
            }

            string[] parts = normalized[6..].Split('/');
            if (parts.Any(p => string.IsNullOrWhiteSpace(p) || p is "." or ".."))
            {
                throw new ForbiddenException("Invalid storage path.");
            }

            target = Path.GetFullPath(Path.Combine(_root, Path.Combine(parts)));
        }

        string prefix = _root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            target.IndexOf(':', OperatingSystem.IsWindows() ? 2 : 0) >= 0)
        {
            throw new ForbiddenException("File path is outside storage.");
        }

        string extension = Path.GetExtension(target);
        if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Only stored images may be accessed.");
        }

        return target;
    }

    private void EnsureNoLinks(string target, bool allowMissing, bool skipLeaf = false)
    {
        string relative = Path.GetRelativePath(_root, target);
        string current = _root;
        string[] segments = relative.Split(Path.DirectorySeparatorChar);
        int last = skipLeaf ? segments.Length - 1 : segments.Length;
        for (int i = -1; i < last; i++)
        {
            if (i >= 0)
            {
                current = Path.Combine(current, segments[i]);
            }

            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ForbiddenException("Storage links are not allowed.");
                }
            }
            catch (FileNotFoundException) when (allowMissing)
            {
                return;
            }
            catch (DirectoryNotFoundException) when (allowMissing)
            {
                return;
            }
        }
    }
}
