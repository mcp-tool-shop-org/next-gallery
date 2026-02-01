using System.Runtime.InteropServices;

namespace Gallery.Infrastructure.Services;

/// <summary>
/// Locates FFmpeg binaries bundled with the application.
/// Priority: bundled (MSIX) → user override → PATH fallback.
/// </summary>
public sealed class FfmpegLocator
{
    private const string OverrideEnvVar = "NEXTGALLERY_FFMPEG_PATH";

    private readonly Lazy<string?> _ffmpegPath;
    private readonly Lazy<string?> _ffprobePath;

    public FfmpegLocator()
    {
        _ffmpegPath = new Lazy<string?>(() => LocateBinary("ffmpeg.exe"));
        _ffprobePath = new Lazy<string?>(() => LocateBinary("ffprobe.exe"));
    }

    /// <summary>
    /// Path to the FFmpeg executable, or null if not found.
    /// </summary>
    public string? FfmpegPath => _ffmpegPath.Value;

    /// <summary>
    /// Path to the FFprobe executable, or null if not found.
    /// </summary>
    public string? FfprobePath => _ffprobePath.Value;

    /// <summary>
    /// Whether FFmpeg is available.
    /// </summary>
    public bool IsAvailable => FfmpegPath is not null;

    private static string? LocateBinary(string binaryName)
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. Bundled path (MSIX layout): {AppDir}/ffmpeg/ffmpeg.exe
        //    In MSIX builds, we use Link to flatten to ffmpeg/ folder
        var bundledPath = Path.Combine(baseDir, "ffmpeg", binaryName);
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        // 2. Dev layout with arch subfolder: {AppDir}/ffmpeg/{arch}/ffmpeg.exe
        var arch = GetArchitectureFolder();
        var devPath = Path.Combine(baseDir, "ffmpeg", arch, binaryName);
        if (File.Exists(devPath))
        {
            return devPath;
        }

        // 3. User override via environment variable
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvVar);
        if (!string.IsNullOrEmpty(overridePath))
        {
            var overrideFile = Path.Combine(overridePath, binaryName);
            if (File.Exists(overrideFile))
            {
                return overrideFile;
            }
        }

        // 4. PATH fallback (dev convenience)
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var pathBinary = Path.Combine(dir, binaryName);
                if (File.Exists(pathBinary))
                {
                    return pathBinary;
                }
            }
        }

        return null;
    }

    private static string GetArchitectureFolder()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            Architecture.X86 => "win-x86",
            _ => "win-x64"
        };
    }
}
