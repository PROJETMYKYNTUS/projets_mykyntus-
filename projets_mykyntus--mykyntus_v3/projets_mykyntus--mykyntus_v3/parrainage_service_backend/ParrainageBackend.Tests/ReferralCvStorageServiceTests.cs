using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using ParrainageBackend.Services;
using Xunit;

namespace ParrainageBackend.Tests;

public sealed class ReferralCvStorageServiceTests : IDisposable
{
    private readonly string _root;
    private readonly ReferralCvStorageService _storage;

    public ReferralCvStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "parrainage-cv-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Parrainage:UploadPath"] = _root })
            .Build();
        var env = new TestWebHostEnvironment();
        _storage = new ReferralCvStorageService(config, env);
    }

    [Fact]
    public async Task SaveAndOpen_ReadsBackFile()
    {
        var file = CreateFormFile("cv.pdf", "application/pdf", "%PDF-1.4 test");
        var url = await _storage.SaveAsync("ref-test-1", file, CancellationToken.None);
        Assert.Equal("/api/parrainage/referrals/ref-test-1/cv", url);

        var opened = _storage.OpenRead("ref-test-1");
        Assert.NotNull(opened);
        using var reader = new StreamReader(opened!.Value.Stream);
        var text = await reader.ReadToEndAsync();
        Assert.Contains("%PDF", text);
        opened.Value.Stream.Dispose();
    }

    [Fact]
    public void ValidateFile_RejectsOversized()
    {
        var big = new byte[10 * 1024 * 1024 + 1];
        var file = CreateFormFile("big.pdf", "application/pdf", big);
        Assert.Throws<InvalidOperationException>(() => _storage.ValidateFile(file));
    }

    [Fact]
    public void ValidateFile_RejectsBadExtension()
    {
        var file = CreateFormFile("virus.exe", "application/octet-stream", new byte[] { 1, 2, 3 });
        Assert.Throws<InvalidOperationException>(() => _storage.ValidateFile(file));
    }

    private static IFormFile CreateFormFile(string name, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static IFormFile CreateFormFile(string name, string contentType, string content) =>
        CreateFormFile(name, contentType, System.Text.Encoding.UTF8.GetBytes(content));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
