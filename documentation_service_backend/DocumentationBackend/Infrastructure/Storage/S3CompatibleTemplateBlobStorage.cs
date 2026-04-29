using System.Net;
using System.Net.Http;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentationBackend.Infrastructure.Storage;

/// <summary>MinIO / S3 — infrastructure.</summary>
public sealed class S3CompatibleTemplateBlobStorage : ITemplateBlobStorage, IDisposable
{
    private const string LocalScheme = "local://";
    private readonly MinioStorageOptions _options;
    private readonly ILogger<S3CompatibleTemplateBlobStorage> _logger;
    private readonly AmazonS3Client? _client;
    private readonly string _localRoot;

    public S3CompatibleTemplateBlobStorage(
        IOptions<DocumentTemplatesInfrastructureOptions> root,
        ILogger<S3CompatibleTemplateBlobStorage> logger)
    {
        _options = root.Value.Minio;
        _logger = logger;
        _localRoot = Path.Combine(AppContext.BaseDirectory, "local-template-storage");
        Directory.CreateDirectory(_localRoot);
        if (IsS3Configured)
        {
            var cfg = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint.TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = string.IsNullOrWhiteSpace(_options.Region) ? "us-east-1" : _options.Region,
            };
            _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, cfg);
        }
    }

    private bool IsS3Configured =>
        !string.IsNullOrWhiteSpace(_options.Endpoint) &&
        !string.IsNullOrWhiteSpace(_options.AccessKey) &&
        !string.IsNullOrWhiteSpace(_options.SecretKey) &&
        !string.IsNullOrWhiteSpace(_options.Bucket);

    /// <summary>
    /// Toujours true : en local on retombe automatiquement sur un stockage fichier
    /// pour éviter de bloquer l'application quand MinIO n'est pas disponible.
    /// </summary>
    public bool IsConfigured => true;

    public async Task<string> PutTemplateObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return await PutLocalObjectAsync(objectKey, content, contentType, cancellationToken).ConfigureAwait(false);

        var bucket = _options.Bucket.Trim();
        try
        {
            await EnsureBucketExistsAsync(bucket, cancellationToken).ConfigureAwait(false);

            var put = new PutObjectRequest
            {
                BucketName = bucket,
                Key = objectKey,
                InputStream = content,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                // Sinon le SDK ferme le flux après l’envoi → "Cannot access a closed Stream" si le même MemoryStream sert à l’analyse DOCX après coup.
                AutoCloseStream = false,
            };
            await _client.PutObjectAsync(put, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "PutObject MinIO/S3 bucket={Bucket} key={Key}", bucket, objectKey);
            return await PutLocalObjectAsync(objectKey, content, contentType, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Réseau indisponible vers MinIO {Endpoint}", _options.Endpoint);
            return await PutLocalObjectAsync(objectKey, content, contentType, cancellationToken).ConfigureAwait(false);
        }

        if (content.CanSeek)
            content.Position = 0;

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return $"{_options.PublicBaseUrl!.TrimEnd('/')}/{objectKey.TrimStart('/')}";

        return $"{_options.Endpoint.TrimEnd('/')}/{bucket}/{objectKey.TrimStart('/')}";
    }

    public string? TryGetPresignedGetUrl(string? storedObjectUri, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(storedObjectUri))
            return null;
        if (storedObjectUri.StartsWith(LocalScheme, StringComparison.OrdinalIgnoreCase))
            return null;
        if (_client is null)
            return null;
        if (!TryParseObjectKey(storedObjectUri, out var key))
        {
            _logger.LogDebug("Presign GET : URI non reconnue pour ce bucket ({Bucket}) : {Uri}", _options.Bucket, storedObjectUri);
            return null;
        }

        try
        {
            var bucket = _options.Bucket.Trim();
            var ttl = lifetime < TimeSpan.FromMinutes(1)
                ? TimeSpan.FromMinutes(1)
                : (lifetime > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : lifetime);
            var req = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(ttl),
            };
            return _client.GetPreSignedURL(req);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Presign GET MinIO/S3 échoué pour clé dérivée de {Uri}", storedObjectUri);
            return null;
        }
    }

    /// <summary>Accepte les URL path-style MinIO : <c>…/bucket/tenant/templates/…</c> ou <c>PublicBaseUrl/key</c>.</summary>
    private bool TryParseObjectKey(string storedObjectUri, out string key)
    {
        key = "";
        if (!Uri.TryCreate(storedObjectUri.Trim(), UriKind.Absolute, out var uri))
            return false;

        var bucket = _options.Bucket.Trim();
        var path = uri.AbsolutePath.TrimStart('/');
        var prefix = bucket + "/";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            key = Uri.UnescapeDataString(path[prefix.Length..]);
            return key.Length > 0;
        }

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            var root = _options.PublicBaseUrl.TrimEnd('/') + "/";
            var stored = storedObjectUri.Trim();
            if (stored.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                key = Uri.UnescapeDataString(stored[root.Length..].TrimStart('/'));
                return key.Length > 0;
            }
        }

        return false;
    }

    public async Task<TemplateBlobPayload?> TryReadObjectAsync(string? storedObjectUri, CancellationToken cancellationToken = default)
    {
        const long maxBytes = 52_428_800;
        if (string.IsNullOrWhiteSpace(storedObjectUri))
            return null;
        if (TryParseLocalObjectPath(storedObjectUri, out var localPath))
        {
            try
            {
                if (!File.Exists(localPath))
                    return null;
                var fi = new FileInfo(localPath);
                if (fi.Length > maxBytes)
                    return null;
                var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
                var fileName = Path.GetFileName(localPath);
                var ct = GuessContentTypeFromFileName(fileName);
                return new TemplateBlobPayload(bytes, ct, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lecture stockage local échouée: {Path}", localPath);
                return null;
            }
        }
        if (_client is null)
            return null;
        if (!TryParseObjectKey(storedObjectUri, out var key))
        {
            _logger.LogDebug("GetObject : URI non reconnue ({Bucket}) : {Uri}", _options.Bucket, storedObjectUri);
            return null;
        }

        var bucket = _options.Bucket.Trim();
        try
        {
            var req = new GetObjectRequest { BucketName = bucket, Key = key };
            using var response = await _client.GetObjectAsync(req, cancellationToken).ConfigureAwait(false);
            if (response.ContentLength > maxBytes)
            {
                _logger.LogWarning("Objet trop volumineux pour lecture API ({Length} o) key={Key}", response.ContentLength, key);
                return null;
            }

            await using var s = response.ResponseStream;
            using var ms = new MemoryStream(response.ContentLength > 0 ? (int)Math.Min(response.ContentLength, maxBytes) : 8192);
            await s.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            if (ms.Length > maxBytes)
                return null;

            var fileName = key.Contains('/') ? key[(key.LastIndexOf('/') + 1)..] : key;
            fileName = Uri.UnescapeDataString(fileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "fichier";

            var ct = !string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? response.Headers.ContentType
                : GuessContentTypeFromFileName(fileName);
            return new TemplateBlobPayload(ms.ToArray(), ct, fileName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode is "NoSuchKey" or "NotFound")
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetObject MinIO/S3 key={Key}", key);
            return null;
        }
    }

    private static string GuessContentTypeFromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            _ => "application/octet-stream",
        };
    }

    private async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct)
    {
        if (_client is null)
            return;
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // ok
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Vérification/création bucket S3 {Bucket}", bucket);
            throw new InvalidOperationException(
                $"MinIO : impossible de créer ou d’accéder au bucket « {bucket} » ({_options.Endpoint}). {ex.ErrorCode}: {ex.Message}",
                ex);
        }
    }

    private async Task<string> PutLocalObjectAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var safeRelative = objectKey.Replace('\\', '/').TrimStart('/');
        var targetPath = Path.Combine(_localRoot, safeRelative.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        if (content.CanSeek)
            content.Position = 0;
        await using (var fs = File.Create(targetPath))
        {
            await content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }
        if (content.CanSeek)
            content.Position = 0;

        var sidecar = targetPath + ".meta";
        var meta = $"contentType={contentType}";
        await File.WriteAllTextAsync(sidecar, meta, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Fallback local storage used for template object: {Path}", targetPath);
        return $"{LocalScheme}{safeRelative}";
    }

    private bool TryParseLocalObjectPath(string storedObjectUri, out string path)
    {
        path = string.Empty;
        if (!storedObjectUri.StartsWith(LocalScheme, StringComparison.OrdinalIgnoreCase))
            return false;
        var relative = storedObjectUri[LocalScheme.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
            return false;
        path = Path.Combine(_localRoot, relative);
        return true;
    }

    public void Dispose() => _client?.Dispose();
}
