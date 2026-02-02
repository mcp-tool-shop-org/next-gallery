using Gallery.Application.Interfaces;
using Gallery.Domain.Enums;
using Gallery.Domain.Models;
using Gallery.Infrastructure.Services;
using NUnit.Framework;

namespace Gallery.Tests;

/// <summary>
/// Tests for MediaActionService - the core AGENCY functionality.
/// </summary>
[TestFixture]
public class MediaActionServiceTests
{
    private string _testDir = null!;
    private string _targetDir = null!;

    [SetUp]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NextGalleryTest_{Guid.NewGuid():N}");
        _targetDir = Path.Combine(Path.GetTempPath(), $"NextGalleryTarget_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_targetDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
            if (Directory.Exists(_targetDir))
                Directory.Delete(_targetDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private string CreateTestFile(string name, string content = "test content")
    {
        var path = Path.Combine(_testDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private MediaItem CreateTestItem(long id, string path)
    {
        return new MediaItem
        {
            Id = id,
            Path = path,
            Extension = Path.GetExtension(path),
            Type = MediaType.Image,
            SizeBytes = File.Exists(path) ? new FileInfo(path).Length : 100,
            ModifiedAt = DateTimeOffset.UtcNow,
            LastIndexedAt = DateTimeOffset.UtcNow
        };
    }

    #region ActionResult Tests

    [Test]
    public void ActionResult_Ok_CreatesSuccessResult()
    {
        var result = ActionResult.Ok(5);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(5));
        Assert.That(result.FailureCount, Is.EqualTo(0));
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ActionResult_Failed_CreatesFailedResult()
    {
        var errors = new List<ActionError>
        {
            new(1, "/path/1.png", "Error 1"),
            new(2, "/path/2.png", "Error 2")
        };

        var result = ActionResult.Failed(errors);

        Assert.That(result.Success, Is.False);
        Assert.That(result.SuccessCount, Is.EqualTo(0));
        Assert.That(result.FailureCount, Is.EqualTo(2));
        Assert.That(result.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public void ActionResult_Partial_CreatesPartialResult()
    {
        var errors = new List<ActionError> { new(1, "/path/1.png", "Error 1") };

        var result = ActionResult.Partial(3, errors);

        Assert.That(result.Success, Is.True); // At least some succeeded
        Assert.That(result.SuccessCount, Is.EqualTo(3));
        Assert.That(result.FailureCount, Is.EqualTo(1));
    }

    #endregion

    #region ActionProgress Tests

    [Test]
    public void ActionProgress_RecordsProgress()
    {
        var progress = new ActionProgress(5, 10, "file.png", TimeSpan.FromSeconds(2));

        Assert.That(progress.Completed, Is.EqualTo(5));
        Assert.That(progress.Total, Is.EqualTo(10));
        Assert.That(progress.CurrentFile, Is.EqualTo("file.png"));
        Assert.That(progress.Elapsed, Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    #endregion

    #region Copy Tests (non-destructive, safe to test)

    [Test]
    public async Task CopyAsync_CopiesSingleFile()
    {
        var sourcePath = CreateTestFile("test.png", "copy test content");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync([item], _targetDir);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(_targetDir, "test.png")), Is.True);
        // Original should still exist
        Assert.That(File.Exists(sourcePath), Is.True);
    }

    [Test]
    public async Task CopyAsync_CopiesMultipleFiles()
    {
        var path1 = CreateTestFile("file1.png");
        var path2 = CreateTestFile("file2.png");
        var path3 = CreateTestFile("file3.png");
        var items = new[]
        {
            CreateTestItem(1, path1),
            CreateTestItem(2, path2),
            CreateTestItem(3, path3)
        };
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync(items, _targetDir);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(3));
        Assert.That(File.Exists(Path.Combine(_targetDir, "file1.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(_targetDir, "file2.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(_targetDir, "file3.png")), Is.True);
    }

    [Test]
    public async Task CopyAsync_HandlesNamingConflicts()
    {
        var sourcePath = CreateTestFile("conflict.png", "source");
        File.WriteAllText(Path.Combine(_targetDir, "conflict.png"), "existing");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync([item], _targetDir);

        Assert.That(result.Success, Is.True);
        // Should create "conflict (1).png"
        Assert.That(File.Exists(Path.Combine(_targetDir, "conflict (1).png")), Is.True);
        // Original "conflict.png" in target should be unchanged
        var existingContent = File.ReadAllText(Path.Combine(_targetDir, "conflict.png"));
        Assert.That(existingContent, Is.EqualTo("existing"));
    }

    [Test]
    public async Task CopyAsync_CreatesTargetDirectoryIfNeeded()
    {
        var sourcePath = CreateTestFile("test.png");
        var item = CreateTestItem(1, sourcePath);
        var newTarget = Path.Combine(_targetDir, "subdir", "nested");
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync([item], newTarget);

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(Path.Combine(newTarget, "test.png")), Is.True);
    }

    [Test]
    public async Task CopyAsync_ReportsProgress()
    {
        var path1 = CreateTestFile("file1.png");
        var path2 = CreateTestFile("file2.png");
        var items = new[] { CreateTestItem(1, path1), CreateTestItem(2, path2) };
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);
        var progressReports = new List<ActionProgress>();

        var progress = new Progress<ActionProgress>(p => progressReports.Add(p));
        var result = await service.CopyAsync(items, _targetDir, progress);

        Assert.That(result.Success, Is.True);
        // Give time for progress events to fire
        await Task.Delay(50);
        Assert.That(progressReports.Count, Is.GreaterThan(0));
        Assert.That(progressReports.Last().Completed, Is.EqualTo(2));
        Assert.That(progressReports.Last().Total, Is.EqualTo(2));
    }

    [Test]
    public async Task CopyAsync_EmptyList_ReturnsOk()
    {
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync([], _targetDir);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CopyAsync_HandlesNonExistentSource()
    {
        var item = CreateTestItem(1, "/nonexistent/path/file.png");
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.CopyAsync([item], _targetDir);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailureCount, Is.EqualTo(1));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("not find"));
    }

    [Test]
    public async Task CopyAsync_SupportsCancellation()
    {
        // Create many files
        var items = Enumerable.Range(1, 100)
            .Select(i =>
            {
                var path = CreateTestFile($"file{i}.png");
                return CreateTestItem(i, path);
            })
            .ToList();

        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(1); // Cancel almost immediately

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.CopyAsync(items, _targetDir, ct: cts.Token));
    }

    #endregion

    #region Move Tests

    [Test]
    public async Task MoveAsync_MovesSingleFile()
    {
        var sourcePath = CreateTestFile("moveme.png", "move content");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.MoveAsync([item], _targetDir);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(_targetDir, "moveme.png")), Is.True);
        Assert.That(File.Exists(sourcePath), Is.False); // Original should be gone
    }

    [Test]
    public async Task MoveAsync_UpdatesDatabase()
    {
        var sourcePath = CreateTestFile("moveme.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        await service.MoveAsync([item], _targetDir);

        Assert.That(mockStore.UpdatedItems, Has.Count.EqualTo(1));
        Assert.That(mockStore.UpdatedItems[0].Path, Does.Contain(_targetDir));
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task DeleteAsync_Permanent_DeletesFile()
    {
        var sourcePath = CreateTestFile("deleteme.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.DeleteAsync([item], permanent: true);

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(sourcePath), Is.False);
        Assert.That(mockStore.DeletedIds, Contains.Item(1L));
    }

    [Test]
    public async Task DeleteAsync_RemovesFromDatabase()
    {
        var sourcePath = CreateTestFile("deleteme.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        await service.DeleteAsync([item], permanent: true);

        Assert.That(mockStore.DeletedIds, Contains.Item(1L));
    }

    [Test]
    public async Task DeleteAsync_EmptyList_ReturnsOk()
    {
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.DeleteAsync([]);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SuccessCount, Is.EqualTo(0));
    }

    #endregion

    #region Rename Tests

    [Test]
    public async Task RenameAsync_RenamesFile()
    {
        var sourcePath = CreateTestFile("oldname.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.RenameAsync(item, "newname.png");

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(Path.Combine(_testDir, "newname.png")), Is.True);
        Assert.That(File.Exists(sourcePath), Is.False);
    }

    [Test]
    public async Task RenameAsync_PreservesExtension()
    {
        var sourcePath = CreateTestFile("test.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.RenameAsync(item, "newname"); // No extension

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(Path.Combine(_testDir, "newname.png")), Is.True);
    }

    [Test]
    public async Task RenameAsync_FailsOnConflict()
    {
        CreateTestFile("existing.png");
        var sourcePath = CreateTestFile("source.png");
        var item = CreateTestItem(1, sourcePath);
        var mockStore = new MockMediaItemStore();
        var service = new MediaActionService(mockStore);

        var result = await service.RenameAsync(item, "existing.png");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("already exists"));
        // Source should still exist
        Assert.That(File.Exists(sourcePath), Is.True);
    }

    #endregion

    /// <summary>
    /// Mock IMediaItemStore for testing without database.
    /// </summary>
    private class MockMediaItemStore : IMediaItemStore
    {
        public List<MediaItem> UpdatedItems { get; } = [];
        public List<long> DeletedIds { get; } = [];

        public Task<MediaItem?> GetByIdAsync(long id, CancellationToken ct = default) => Task.FromResult<MediaItem?>(null);
        public Task<MediaItem?> GetByPathAsync(string path, CancellationToken ct = default) => Task.FromResult<MediaItem?>(null);
        public Task<IReadOnlyList<MediaItem>> GetAllAsync(int limit = 1000, int offset = 0, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetFavoritesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<QueryResult> QueryAsync(LibraryQuery query, int limit = 1000, int offset = 0, CancellationToken ct = default)
            => Task.FromResult(new QueryResult([], 0));
        public Task<GroupedQueryResult> QueryGroupedAsync(LibraryQuery query, int limit = 1000, CancellationToken ct = default)
            => Task.FromResult(new GroupedQueryResult([], 0));
        public Task<long> UpsertAsync(MediaItem item, CancellationToken ct = default) => Task.FromResult(item.Id);
        public Task UpdateAsync(MediaItem item, CancellationToken ct = default)
        {
            UpdatedItems.Add(item);
            return Task.CompletedTask;
        }
        public Task UpdateThumbPathAsync(long id, ThumbSize size, string thumbPath, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task SetFavoriteAsync(long id, bool isFavorite, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetRatingAsync(long id, int rating, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken ct = default)
        {
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }
        public Task DeleteByPathAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetCountAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
