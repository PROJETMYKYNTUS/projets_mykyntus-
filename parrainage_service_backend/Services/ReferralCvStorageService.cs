namespace ParrainageBackend.Services;

/// <summary>Stores referral CV files on disk (Docker volume).</summary>
public sealed class ReferralCvStorageService(IConfiguration configuration, IWebHostEnvironment env)
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx",
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public string UploadRoot =>
        configuration["Parrainage:UploadPath"]
        ?? Path.Combine(env.ContentRootPath, "uploads", "cv");

    public static string CvApiPath(string referralId) => $"/api/parrainage/referrals/{referralId}/cv";

    public void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Aucun fichier fourni.");

        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Le fichier dépasse la taille maximale de 10 Mo.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Format non autorisé. Utilisez PDF, DOC ou DOCX.");
    }

    public async Task<string> SaveAsync(string referralId, IFormFile file, CancellationToken ct)
    {
        ValidateFile(file);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        Directory.CreateDirectory(UploadRoot);
        var path = GetFilePath(referralId, ext);
        if (File.Exists(path))
            File.Delete(path);
        // Remove other extensions for same referral
        foreach (var other in AllowedExtensions.Where(e => e != ext))
        {
            var alt = GetFilePath(referralId, other);
            if (File.Exists(alt)) File.Delete(alt);
        }

        await using var stream = File.Create(path);
        await file.CopyToAsync(stream, ct);
        return CvApiPath(referralId);
    }

    public (Stream Stream, string ContentType, string FileName)? OpenRead(string referralId)
    {
        foreach (var ext in AllowedExtensions)
        {
            var path = GetFilePath(referralId, ext);
            if (!File.Exists(path)) continue;
            var stream = File.OpenRead(path);
            var contentType = ContentTypes.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
            return (stream, contentType, $"cv-{referralId}{ext}");
        }
        return null;
    }

    public bool Exists(string referralId) =>
        AllowedExtensions.Any(ext => File.Exists(GetFilePath(referralId, ext)));

    public void Delete(string referralId)
    {
        foreach (var ext in AllowedExtensions)
        {
            var path = GetFilePath(referralId, ext);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private string GetFilePath(string referralId, string extension)
    {
        var safeId = string.Concat(referralId.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (string.IsNullOrEmpty(safeId)) safeId = "unknown";
        return Path.Combine(UploadRoot, $"{safeId}{extension}");
    }
}
