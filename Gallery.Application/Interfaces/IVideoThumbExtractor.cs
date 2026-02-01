namespace Gallery.Application.Interfaces;

/// <summary>
/// Extracts thumbnail frames from video files.
/// This is an optional capability - implementations may not be available
/// if the required video processing tools (e.g., FFmpeg) are not installed.
/// </summary>
public interface IVideoThumbExtractor
{
    /// <summary>
    /// Extract a JPEG frame from a video at the specified time.
    /// </summary>
    /// <param name="videoPath">Path to the video file</param>
    /// <param name="atSeconds">Time offset in seconds to extract frame</param>
    /// <param name="maxPixels">Maximum dimension (width or height)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JPEG bytes, or empty array if extraction failed</returns>
    Task<byte[]> ExtractFrameJpegAsync(
        string videoPath,
        double atSeconds,
        int maxPixels,
        CancellationToken ct = default);

    /// <summary>
    /// Whether the video extractor is available (has required dependencies).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Get the recommended extraction time for a video based on its duration.
    /// Returns time at 10% of duration, clamped between 1-30 seconds.
    /// </summary>
    /// <param name="durationSeconds">Total video duration in seconds</param>
    /// <returns>Recommended extraction time in seconds</returns>
    double GetRecommendedExtractionTime(double durationSeconds);
}
