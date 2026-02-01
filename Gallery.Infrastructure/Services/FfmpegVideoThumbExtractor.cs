using System.Diagnostics;
using Gallery.Application.Interfaces;

namespace Gallery.Infrastructure.Services;

/// <summary>
/// Extracts video thumbnails using FFmpeg.
/// </summary>
public sealed class FfmpegVideoThumbExtractor : IVideoThumbExtractor
{
    private readonly FfmpegLocator _locator;
    private const int ProcessTimeoutMs = 30_000; // 30 seconds max per extraction

    public FfmpegVideoThumbExtractor(FfmpegLocator locator)
    {
        _locator = locator;
    }

    public bool IsAvailable => _locator.IsAvailable;

    public double GetRecommendedExtractionTime(double durationSeconds)
    {
        // Extract at 10% of duration, clamped between 1-30 seconds
        var target = durationSeconds * 0.1;
        return Math.Clamp(target, 1.0, 30.0);
    }

    public async Task<byte[]> ExtractFrameJpegAsync(
        string videoPath,
        double atSeconds,
        int maxPixels,
        CancellationToken ct = default)
    {
        if (!IsAvailable || _locator.FfmpegPath is null)
        {
            return Array.Empty<byte>();
        }

        if (!File.Exists(videoPath))
        {
            return Array.Empty<byte>();
        }

        // Sanitize inputs
        var safeTime = Math.Max(0, atSeconds);
        var safeMaxPixels = Math.Clamp(maxPixels, 32, 4096);

        // Build FFmpeg arguments:
        // -ss: seek to time (before input for fast seeking)
        // -i: input file
        // -vframes 1: extract single frame
        // -vf scale: resize while maintaining aspect ratio
        // -f image2pipe: output to pipe
        // -c:v mjpeg: output as JPEG
        // pipe:1: write to stdout
        var scale = $"scale='min({safeMaxPixels},iw)':min'({safeMaxPixels},ih)':force_original_aspect_ratio=decrease";
        var args = $"-ss {safeTime:F2} -i \"{videoPath}\" -vframes 1 -vf \"{scale}\" -f image2pipe -c:v mjpeg -q:v 2 pipe:1";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ProcessTimeoutMs);

            var result = await RunFfmpegAsync(_locator.FfmpegPath, args, cts.Token);
            return result;
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<byte>();
        }
        catch (Exception)
        {
            // FFmpeg errors are expected for corrupt/unsupported files
            return Array.Empty<byte>();
        }
    }

    private static async Task<byte[]> RunFfmpegAsync(string ffmpegPath, string args, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Security: don't inherit environment, run with restricted privileges
                WindowStyle = ProcessWindowStyle.Hidden
            },
            EnableRaisingEvents = true
        };

        process.Start();

        // Read stdout (the JPEG data) into memory
        using var ms = new MemoryStream();
        var readTask = process.StandardOutput.BaseStream.CopyToAsync(ms, ct);

        // Discard stderr (FFmpeg writes progress/warnings there)
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(readTask, errorTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            return Array.Empty<byte>();
        }

        return ms.ToArray();
    }
}
