using System.Diagnostics;
using System.Runtime.InteropServices;
using Gallery.Application.Interfaces;
using Gallery.Domain.Models;

namespace Gallery.Infrastructure.Services;

/// <summary>
/// Implementation of media actions - delete, move, copy, rename.
/// Uses shell operations for trash support on Windows.
/// </summary>
public sealed class MediaActionService : IMediaActionService
{
    private readonly IMediaItemStore _itemStore;

    public MediaActionService(IMediaItemStore itemStore)
    {
        _itemStore = itemStore;
    }

    public async Task<ActionResult> DeleteAsync(
        IEnumerable<MediaItem> items,
        bool permanent = false,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return ActionResult.Ok(0);

        var errors = new List<ActionError>();
        var completed = 0;
        var sw = Stopwatch.StartNew();

        foreach (var item in itemList)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (permanent)
                {
                    // Permanent delete
                    if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                    }
                }
                else
                {
                    // Move to trash (Windows-specific)
                    if (!MoveToTrash(item.Path))
                    {
                        // Fallback to permanent delete if trash fails
                        if (File.Exists(item.Path))
                        {
                            File.Delete(item.Path);
                        }
                    }
                }

                // Remove from database
                await _itemStore.DeleteAsync(item.Id, ct);
                completed++;
            }
            catch (Exception ex)
            {
                errors.Add(new ActionError(item.Id, item.Path, ex.Message));
            }

            progress?.Report(new ActionProgress(
                completed + errors.Count,
                itemList.Count,
                Path.GetFileName(item.Path),
                sw.Elapsed));
        }

        return errors.Count == 0
            ? ActionResult.Ok(completed)
            : ActionResult.Partial(completed, errors);
    }

    public async Task<ActionResult> MoveAsync(
        IEnumerable<MediaItem> items,
        string targetFolder,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return ActionResult.Ok(0);

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var errors = new List<ActionError>();
        var completed = 0;
        var sw = Stopwatch.StartNew();

        foreach (var item in itemList)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fileName = Path.GetFileName(item.Path);
                var targetPath = GetUniqueFilePath(targetFolder, fileName);

                File.Move(item.Path, targetPath);

                // Update database with new path
                var updated = item with { Path =targetPath };
                await _itemStore.UpdateAsync(updated, ct);
                completed++;
            }
            catch (Exception ex)
            {
                errors.Add(new ActionError(item.Id, item.Path, ex.Message));
            }

            progress?.Report(new ActionProgress(
                completed + errors.Count,
                itemList.Count,
                Path.GetFileName(item.Path),
                sw.Elapsed));
        }

        return errors.Count == 0
            ? ActionResult.Ok(completed)
            : ActionResult.Partial(completed, errors);
    }

    public async Task<ActionResult> CopyAsync(
        IEnumerable<MediaItem> items,
        string targetFolder,
        IProgress<ActionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return ActionResult.Ok(0);

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        var errors = new List<ActionError>();
        var completed = 0;
        var sw = Stopwatch.StartNew();

        foreach (var item in itemList)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fileName = Path.GetFileName(item.Path);
                var targetPath = GetUniqueFilePath(targetFolder, fileName);

                File.Copy(item.Path, targetPath);
                completed++;

                // Note: We don't add the copy to the database - it will be found on next scan
                // if the target folder is in a library folder
            }
            catch (Exception ex)
            {
                errors.Add(new ActionError(item.Id, item.Path, ex.Message));
            }

            progress?.Report(new ActionProgress(
                completed + errors.Count,
                itemList.Count,
                Path.GetFileName(item.Path),
                sw.Elapsed));
        }

        return errors.Count == 0
            ? ActionResult.Ok(completed)
            : ActionResult.Partial(completed, errors);
    }

    public async Task<ActionResult> RenameAsync(
        MediaItem item,
        string newName,
        CancellationToken ct = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(item.Path)!;
            var extension = Path.GetExtension(item.Path);

            // Ensure extension is preserved
            if (!newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                newName += extension;
            }

            var newPath = Path.Combine(directory, newName);

            // Check for conflicts
            if (File.Exists(newPath) && !string.Equals(item.Path, newPath, StringComparison.OrdinalIgnoreCase))
            {
                return ActionResult.Failed([new ActionError(item.Id, item.Path, $"A file named '{newName}' already exists")]);
            }

            File.Move(item.Path, newPath);

            // Update database
            var updated = item with { Path =newPath };
            await _itemStore.UpdateAsync(updated, ct);

            return ActionResult.Ok(1);
        }
        catch (Exception ex)
        {
            return ActionResult.Failed([new ActionError(item.Id, item.Path, ex.Message)]);
        }
    }

    public Task OpenInExplorerAsync(MediaItem item)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{item.Path}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var directory = Path.GetDirectoryName(item.Path);
            if (directory != null)
            {
                Process.Start("xdg-open", directory);
            }
        }

        return Task.CompletedTask;
    }

    public Task OpenWithDefaultAppAsync(MediaItem item)
    {
        if (File.Exists(item.Path))
        {
            var psi = new ProcessStartInfo(item.Path)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get a unique file path, adding (1), (2), etc. if the file exists.
    /// </summary>
    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
            return path;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 1;

        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"{nameWithoutExt} ({counter}){ext}");
            counter++;
        }

        return path;
    }

    /// <summary>
    /// Move a file to the Windows Recycle Bin using shell API.
    /// </summary>
    private static bool MoveToTrash(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        if (!File.Exists(filePath))
            return true; // Already gone

        try
        {
            // Use Microsoft.VisualBasic for trash support (available in .NET)
            // Note: This requires adding Microsoft.VisualBasic reference
            // For now, we'll use a P/Invoke approach

            var shFileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = filePath + '\0' + '\0', // Double null-terminated
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            var result = SHFileOperation(ref shFileOp);
            return result == 0;
        }
        catch
        {
            return false;
        }
    }

    #region Windows Shell P/Invoke

    private const int FO_DELETE = 0x0003;
    private const int FOF_ALLOWUNDO = 0x0040;
    private const int FOF_NOCONFIRMATION = 0x0010;
    private const int FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public short fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    #endregion
}
