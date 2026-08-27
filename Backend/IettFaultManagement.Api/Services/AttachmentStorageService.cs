namespace IettFaultManagement.Api.Services;

/// <summary>Diske kaydedilen dosyanın veritabanına yazılacak güvenli metadata bilgilerini taşır.</summary>
public sealed record StoredAttachment(
    string OriginalFileName,
    string StoredFileName,
    string RelativePath,
    string ContentType,
    long FileSize);

/// <summary>
/// Yüklenen dosyanın uzantı, MIME türü ve boyutunu doğrular; dosyayı kullanıcı adından
/// bağımsız benzersiz bir adla App_Data altında saklar ve güvenli biçimde geri okur.
/// </summary>
public sealed class AttachmentStorageService(IWebHostEnvironment environment, IConfiguration configuration)
{
    public const long MaximumFileSize = 20 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string[]> AllowedTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"],
            [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"],
            [".mp4"] = ["video/mp4"]
        };

    private string RootPath => Path.GetFullPath(Path.Combine(
        environment.ContentRootPath,
        configuration["Storage:Root"] ?? "App_Data/Uploads"));

    public async Task<StoredAttachment> SaveAsync(
        IFormFile file,
        string group,
        long ownerId,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new ArgumentException("Boş dosya yüklenemez.");
        if (file.Length > MaximumFileSize) throw new ArgumentException("Dosya boyutu 20 MB sınırını aşamaz.");

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var contentTypes) ||
            !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Yalnızca JPG, PNG, WEBP, PDF veya MP4 dosyaları yüklenebilir.");

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(group, ownerId.ToString(), storedName).Replace('\\', '/');
        var absolutePath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredAttachment(originalName, storedName, relativePath, file.ContentType, file.Length);
    }

    public FileStream OpenRead(string relativePath) =>
        new(ResolvePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

    public void DeleteIfExists(string relativePath)
    {
        var path = ResolvePath(relativePath);
        if (File.Exists(path)) File.Delete(path);
    }

    private string ResolvePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        if (!path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        return path;
    }
}
