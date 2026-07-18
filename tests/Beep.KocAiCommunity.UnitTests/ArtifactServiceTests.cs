using System.Text;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public sealed class ArtifactServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KocDbContext _db;
    private readonly string _root;

    public ArtifactServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new KocDbContext(new DbContextOptionsBuilder<KocDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _root = Path.Combine(Path.GetTempPath(), "koc-artifact-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private ArtifactService CreateService(ArtifactUploadOptions? options = null)
    {
        options ??= new ArtifactUploadOptions();
        options.RootPath = _root;
        return new ArtifactService(_db, new LocalFileArtifactStore(_root), Options.Create(options));
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Save_records_hash_size_classification_and_roundtrips()
    {
        var service = CreateService();

        var reference = await service.SaveAsync(Bytes("hello KOC"), "well.csv", "text/csv", KocDataClassification.Confidential);

        reference.Sha256.Should().HaveLength(64);
        reference.SizeBytes.Should().Be(9);
        reference.Classification.Should().Be(KocDataClassification.Confidential);

        await using var read = await service.OpenReadAsync(reference.Id);
        using var sr = new StreamReader(read);
        (await sr.ReadToEndAsync()).Should().Be("hello KOC");
    }

    [Fact]
    public async Task Identical_bytes_are_deduplicated()
    {
        var service = CreateService();

        var first = await service.SaveAsync(Bytes("same"), "a.csv", "text/csv", KocDataClassification.Internal);
        var second = await service.SaveAsync(Bytes("same"), "b.csv", "text/csv", KocDataClassification.Internal);

        second.Id.Should().Be(first.Id);
        (await _db.ArtifactReferences.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Oversize_upload_is_rejected()
    {
        var service = CreateService(new ArtifactUploadOptions { MaxSizeBytes = 4 });

        var act = () => service.SaveAsync(Bytes("too many bytes"), "big.csv", "text/csv", KocDataClassification.Internal);

        await act.Should().ThrowAsync<ArtifactValidationException>();
    }

    [Fact]
    public async Task Disallowed_extension_is_rejected()
    {
        var options = new ArtifactUploadOptions();
        options.AllowedExtensions.Add(".csv");
        var service = CreateService(options);

        var act = () => service.SaveAsync(Bytes("x"), "script.exe", "application/octet-stream", KocDataClassification.Internal);

        await act.Should().ThrowAsync<ArtifactValidationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
