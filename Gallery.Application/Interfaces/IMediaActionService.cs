using Gallery.Domain.Models;

namespace Gallery.Application.Interfaces;

/// <summary>
/// Service for performing actions on media items.
/// This provides the AGENCY that was missing - delete, move, copy, rename.
/// </summary>
public interface IMediaActionService
{
    /// <summary>
    /// Delete media items. By default moves to system trash for safety.
    /// </summary>
    /// <param name="items">Items to delete</param>
    /// <param name="permanent">If true, permanently delete. If false, move to trash.</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with success count and any errors</returns>
    Task<ActionResult> DeleteAsync(
        IEnumerable<MediaItem> items,
        bool permanent = false,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Move media items to a target folder.
    /// </summary>
    Task<ActionResult> MoveAsync(
        IEnumerable<MediaItem> items,
        string targetFolder,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Copy media items to a target folder.
    /// </summary>
    Task<ActionResult> CopyAsync(
        IEnumerable<MediaItem> items,
        string targetFolder,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Rename a single media item.
    /// </summary>
    Task<ActionResult> RenameAsync(
        MediaItem item,
        string newName,
        CancellationToken ct = default);

    /// <summary>
    /// Open the containing folder for an item in the system file explorer.
    /// </summary>
    Task OpenInExplorerAsync(MediaItem item);

    /// <summary>
    /// Open an item with the system default application.
    /// </summary>
    Task OpenWithDefaultAppAsync(MediaItem item);
}

/// <summary>
/// Result of a media action operation.
/// </summary>
public record ActionResult(
    bool Success,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ActionError> Errors)
{
    public static ActionResult Ok(int count) => new(true, count, 0, []);
    public static ActionResult Failed(IReadOnlyList<ActionError> errors)
        => new(false, 0, errors.Count, errors);
    public static ActionResult Partial(int success, IReadOnlyList<ActionError> errors)
        => new(success > 0, success, errors.Count, errors);
}

/// <summary>
/// Error detail for a failed action on a specific item.
/// </summary>
public record ActionError(
    long ItemId,
    string FilePath,
    string ErrorMessage);

/// <summary>
/// Progress report for batch actions.
/// </summary>
public record ActionProgress(
    int Completed,
    int Total,
    string CurrentFile,
    TimeSpan Elapsed);
