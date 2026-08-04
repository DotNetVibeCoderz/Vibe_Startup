using System.Text;
using FastRide.Api.Infrastructure;
using FastRide.Shared.Storage;
using Microsoft.Extensions.Configuration;

namespace FastRide.Tests.Unit;

public class FileSystemStorageProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fastride-storage-{Guid.NewGuid():N}");
    private readonly FileSystemStorageProvider _storage;

    public FileSystemStorageProviderTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:FileSystem:Path"] = _root,
                ["Storage:FileSystem:BaseUrl"] = "/uploads"
            })
            .Build();

        _storage = new FileSystemStorageProvider(config);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception)
        {
            // Temp directory; not worth failing over.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Upload_ThenDownload_RoundTrips()
    {
        var payload = Encoding.UTF8.GetBytes("halo dunia");

        var url = await _storage.UploadAsync("photos/test.txt", payload, "text/plain");
        var readBack = await _storage.DownloadAsync("photos/test.txt");

        Assert.Equal("/uploads/photos/test.txt", url);
        Assert.Equal(payload, readBack);
    }

    [Fact]
    public async Task Download_ReturnsNull_ForAMissingFile() =>
        Assert.Null(await _storage.DownloadAsync("photos/never-written.txt"));

    [Fact]
    public async Task Exists_ReflectsWhetherTheFileIsThere()
    {
        Assert.False(await _storage.ExistsAsync("photos/x.bin"));

        await _storage.UploadAsync("photos/x.bin", [1, 2, 3], "application/octet-stream");

        Assert.True(await _storage.ExistsAsync("photos/x.bin"));
    }

    [Fact]
    public async Task Delete_RemovesTheFileAndReportsWhetherItDidAnything()
    {
        await _storage.UploadAsync("photos/y.bin", [1], "application/octet-stream");

        Assert.True(await _storage.DeleteAsync("photos/y.bin"));
        Assert.False(await _storage.DeleteAsync("photos/y.bin"));
    }

    [Fact]
    public async Task Upload_RefusesToEscapeTheUploadDirectory()
    {
        // A crafted file name must not be able to write over application files.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.UploadAsync("../../appsettings.json", [1], "application/json"));
    }

    [Fact]
    public void ResolveFileName_RecoversTheStorageKeyFromItsOwnUrl() =>
        Assert.Equal("photos/abc.jpg", _storage.ResolveFileName("/uploads/photos/abc.jpg"));

    [Fact]
    public void ResolveFileName_HandlesAnAbsoluteUrl() =>
        Assert.Equal("photos/abc.jpg", _storage.ResolveFileName("https://cdn.example.com/uploads/photos/abc.jpg"));

    [Fact]
    public void ResolveFileName_ReturnsNull_ForAGeneratedAvatar()
    {
        // Generated avatars are inline data URIs. The old delete path ran `new Uri(...)` on
        // these and tried to remove a file that never existed.
        Assert.Null(_storage.ResolveFileName("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4="));
    }

    [Fact]
    public void ResolveFileName_ReturnsNull_ForSomeoneElsesUrl() =>
        Assert.Null(_storage.ResolveFileName("https://example.com/media/photo.jpg"));

    [Fact]
    public void ResolveFileName_ReturnsNull_ForNothing() =>
        Assert.Null(_storage.ResolveFileName(string.Empty));

    [Fact]
    public void GeneratePhotoFileName_IsUniqueAndLandsInThePhotosFolder()
    {
        IStorageProvider storage = _storage;
        var userId = Guid.NewGuid();

        var first = storage.GeneratePhotoFileName(userId, ".JPG");
        var second = storage.GeneratePhotoFileName(userId, ".JPG");

        Assert.StartsWith("photos/", first);
        Assert.EndsWith(".jpg", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateDocumentFileName_SeparatesDocumentsByType()
    {
        IStorageProvider storage = _storage;

        var licence = storage.GenerateDocumentFileName(Guid.NewGuid(), "DriverLicense", "png");

        Assert.StartsWith("documents/driverlicense/", licence);
        Assert.EndsWith(".png", licence);
    }

    [Fact]
    public void GenerateFileName_FallsBackToABinaryExtension()
    {
        IStorageProvider storage = _storage;

        Assert.EndsWith(".bin", storage.GeneratePhotoFileName(Guid.NewGuid(), "   "));
    }
}
