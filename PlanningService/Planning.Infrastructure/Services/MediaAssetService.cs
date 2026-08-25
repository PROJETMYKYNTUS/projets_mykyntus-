using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public class MediaAssetService : IMediaAssetService
{
    private static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private static readonly HashSet<string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm", "video/quicktime"
    };

    private static readonly HashSet<string> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<MediaAssetService> _logger;

    public MediaAssetService(AppDbContext db, IConfiguration config, ILogger<MediaAssetService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private string RootPath =>
        _config["Planning:Media:RootPath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads", "media");

    private long MaxImageBytes =>
        long.TryParse(_config["Planning:Media:MaxImageBytes"], out var v) ? v : 10_000_000;

    private long MaxVideoBytes =>
        long.TryParse(_config["Planning:Media:MaxVideoBytes"], out var v) ? v : 150_000_000;

    private long MaxDocumentBytes =>
        long.TryParse(_config["Planning:Media:MaxDocumentBytes"], out var v) ? v : 20_000_000;

    public async Task<MediaAssetDto> UploadAsync(IFormFile file, string userId, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("Fichier manquant.");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        var kind = InferKind(file.FileName, contentType);
        var max = kind switch
        {
            MediaKind.Image => MaxImageBytes,
            MediaKind.Video => MaxVideoBytes,
            _ => MaxDocumentBytes
        };
        if (file.Length > max)
            throw new InvalidOperationException($"Fichier trop volumineux (max {max / 1_000_000} Mo pour {kind}).");

        Directory.CreateDirectory(RootPath);
        var safeName = SanitizeFileName(file.FileName);
        var relative = Path.Combine(DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"),
            $"{Guid.NewGuid():N}_{safeName}");
        var fullPath = Path.Combine(RootPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var entity = new MediaAsset
        {
            OwnerType = MediaOwnerType.Orphan,
            OwnerId = null,
            Kind = kind,
            FileName = safeName,
            ContentType = contentType,
            StoragePath = relative.Replace('\\', '/'),
            SizeBytes = file.Length,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.MediaAssets.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Media {Id} uploaded by {User} ({Kind}, {Size} bytes)", entity.Id, userId, kind, file.Length);
        return MediaDtoMapper.ToDto(entity);
    }

    public async Task AttachAsync(IEnumerable<int> mediaIds, MediaOwnerType ownerType, int ownerId, CancellationToken ct = default)
    {
        var ids = mediaIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var assets = await _db.MediaAssets
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

        var order = 0;
        foreach (var id in ids)
        {
            var asset = assets.FirstOrDefault(a => a.Id == id);
            if (asset is null) continue;
            if (asset.OwnerType != MediaOwnerType.Orphan &&
                !(asset.OwnerType == ownerType && asset.OwnerId == ownerId))
            {
                throw new InvalidOperationException($"Le média {id} est déjà attaché à un autre objet.");
            }

            asset.OwnerType = ownerType;
            asset.OwnerId = ownerId;
            asset.SortOrder = order++;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MediaAssetDto>> ListByOwnerAsync(MediaOwnerType ownerType, int ownerId, CancellationToken ct = default)
    {
        var list = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.OwnerType == ownerType && m.OwnerId == ownerId)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);
        return list.Select(MediaDtoMapper.ToDto).ToList();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(int id, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (asset is null) return null;

        var fullPath = Path.Combine(RootPath, asset.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return null;

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, asset.ContentType, asset.FileName);
    }

    public async Task<bool> DeleteAsync(int id, string userId, bool allowAdmin, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (asset is null) return false;
        if (!allowAdmin && asset.UploadedByUserId != userId)
            throw new UnauthorizedAccessException("Suppression non autorisée.");

        var fullPath = Path.Combine(RootPath, asset.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(ct);

        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de supprimer le fichier {Path}", fullPath);
        }

        return true;
    }

    private static MediaKind InferKind(string fileName, string contentType)
    {
        if (ImageTypes.Contains(contentType) || LooksLike(fileName, ".jpg", ".jpeg", ".png", ".webp", ".gif"))
            return MediaKind.Image;
        if (VideoTypes.Contains(contentType) || LooksLike(fileName, ".mp4", ".webm", ".mov"))
            return MediaKind.Video;
        if (DocumentTypes.Contains(contentType) || LooksLike(fileName, ".pdf", ".doc", ".docx", ".xls", ".xlsx"))
            return MediaKind.Document;
        throw new InvalidOperationException("Type de fichier non supporté. Images, vidéos ou PDF/Office uniquement.");
    }

    private static bool LooksLike(string fileName, params string[] exts) =>
        exts.Any(e => fileName.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static string SanitizeFileName(string name)
    {
        var baseName = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return string.IsNullOrWhiteSpace(baseName) ? "file.bin" : baseName;
    }
}
