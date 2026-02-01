#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads and verifies FFmpeg binaries for NextGallery.

.DESCRIPTION
    Fetches FFmpeg essentials builds (LGPL) for win-x64 and win-arm64,
    verifies SHA-256 checksums, and extracts to the Assets folder.

.PARAMETER Architecture
    Specify 'x64', 'arm64', or 'all' (default: all)

.PARAMETER Force
    Re-download even if binaries already exist

.EXAMPLE
    .\fetch-ffmpeg.ps1
    .\fetch-ffmpeg.ps1 -Architecture x64
    .\fetch-ffmpeg.ps1 -Force
#>
param(
    [ValidateSet('x64', 'arm64', 'all')]
    [string]$Architecture = 'all',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Configuration - UPDATE THESE for each FFmpeg release
$FfmpegVersion = "7.1"
$Builds = @{
    'x64' = @{
        Url = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-${FfmpegVersion}-essentials_build.zip"
        # UPDATE: Get actual SHA256 from gyan.dev after downloading
        Sha256 = "UPDATE_WITH_ACTUAL_SHA256_HASH"
        ExtractPath = "ffmpeg-${FfmpegVersion}-essentials_build"
    }
    'arm64' = @{
        # Note: gyan.dev may not have ARM64 builds - check official FFmpeg or use alternative
        # For now, using placeholder - ARM64 Windows builds are less common
        Url = $null  # Set to actual URL when available
        Sha256 = $null
        ExtractPath = $null
    }
}

$ScriptRoot = $PSScriptRoot
$RepoRoot = Split-Path $ScriptRoot -Parent
$AssetsDir = Join-Path $RepoRoot "Gallery.App\Assets\ffmpeg"

function Get-FileHash256 {
    param([string]$Path)
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLower()
}

function Install-FfmpegArch {
    param(
        [string]$Arch,
        [hashtable]$BuildInfo
    )

    if (-not $BuildInfo.Url) {
        Write-Warning "No FFmpeg build available for $Arch - skipping"
        return
    }

    $targetDir = Join-Path $AssetsDir "win-$Arch"
    $ffmpegExe = Join-Path $targetDir "ffmpeg.exe"
    $ffprobeExe = Join-Path $targetDir "ffprobe.exe"

    # Check if already exists
    if (-not $Force -and (Test-Path $ffmpegExe) -and (Test-Path $ffprobeExe)) {
        Write-Host "FFmpeg for $Arch already exists. Use -Force to re-download." -ForegroundColor Yellow
        return
    }

    # Create temp directory
    $tempDir = Join-Path $env:TEMP "ffmpeg-download-$Arch"
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    try {
        $zipFile = Join-Path $tempDir "ffmpeg.zip"

        # Download
        Write-Host "Downloading FFmpeg $Arch..." -ForegroundColor Cyan
        $ProgressPreference = 'SilentlyContinue'  # Faster download
        Invoke-WebRequest -Uri $BuildInfo.Url -OutFile $zipFile -UseBasicParsing
        $ProgressPreference = 'Continue'

        # Verify hash
        Write-Host "Verifying SHA256..." -ForegroundColor Cyan
        $actualHash = Get-FileHash256 -Path $zipFile
        if ($BuildInfo.Sha256 -and $BuildInfo.Sha256 -ne "UPDATE_WITH_ACTUAL_SHA256_HASH") {
            if ($actualHash -ne $BuildInfo.Sha256.ToLower()) {
                throw "SHA256 mismatch! Expected: $($BuildInfo.Sha256), Got: $actualHash"
            }
            Write-Host "SHA256 verified: $actualHash" -ForegroundColor Green
        } else {
            Write-Warning "SHA256 not configured - update script with: $actualHash"
        }

        # Extract
        Write-Host "Extracting..." -ForegroundColor Cyan
        Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force

        # Find binaries in extracted folder
        $extractedDir = Join-Path $tempDir $BuildInfo.ExtractPath
        $binDir = Join-Path $extractedDir "bin"

        if (-not (Test-Path $binDir)) {
            # Try finding it
            $binDir = Get-ChildItem -Path $tempDir -Recurse -Directory -Filter "bin" | Select-Object -First 1 -ExpandProperty FullName
        }

        if (-not $binDir -or -not (Test-Path $binDir)) {
            throw "Could not find bin directory in extracted archive"
        }

        # Create target directory
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir | Out-Null
        }

        # Copy binaries
        Copy-Item (Join-Path $binDir "ffmpeg.exe") -Destination $ffmpegExe -Force
        Copy-Item (Join-Path $binDir "ffprobe.exe") -Destination $ffprobeExe -Force

        Write-Host "Installed FFmpeg $Arch to $targetDir" -ForegroundColor Green

    } finally {
        # Cleanup
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# Main
Write-Host "=== NextGallery FFmpeg Fetcher ===" -ForegroundColor Magenta
Write-Host "Version: $FfmpegVersion"
Write-Host "Target: $AssetsDir"
Write-Host ""

# Ensure Assets directory exists
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir | Out-Null
}

# Download requested architectures
if ($Architecture -eq 'all') {
    Install-FfmpegArch -Arch 'x64' -BuildInfo $Builds['x64']
    Install-FfmpegArch -Arch 'arm64' -BuildInfo $Builds['arm64']
} else {
    Install-FfmpegArch -Arch $Architecture -BuildInfo $Builds[$Architecture]
}

Write-Host ""
Write-Host "Done! Remember to update FFMPEG-BUILD-INFO.txt with the SHA256 hashes." -ForegroundColor Yellow
