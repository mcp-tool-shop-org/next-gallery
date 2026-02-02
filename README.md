# NextGallery

[![Build](https://github.com/mcp-tool-shop-org/next-gallery/actions/workflows/build.yml/badge.svg)](https://github.com/mcp-tool-shop-org/next-gallery/actions/workflows/build.yml)
[![Tests](https://github.com/mcp-tool-shop-org/next-gallery/actions/workflows/test.yml/badge.svg)](https://github.com/mcp-tool-shop-org/next-gallery/actions/workflows/test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**NextGallery** is a high-performance Windows desktop application for browsing and managing AI-generated images and videos. Built with .NET MAUI and WinUI 3, it provides a native Windows experience with smooth scrolling, instant previews, and powerful organization tools.

## Features

### Core Features
- **High Performance** - Virtualized grid with lazy loading for thousands of images
- **Instant Previews** - Quick hover previews without opening files
- **Smart Organization** - Filter by date, prompt, model, or custom tags
- **Metadata Display** - View generation parameters, prompts, and settings
- **ComfyUI Integration** - Works seamlessly with [CodeComfy VS Code](https://github.com/mcp-tool-shop-org/codecomfy-vscode)
- **Video Support** - Preview and organize AI-generated videos

### 2026 Features (New!)

#### Job Management (Agency)
- **Delete Jobs** - Remove jobs from index with optional file deletion
- **Open in Explorer** - Quick access to job output files
- **Copy Prompt** - One-click copy prompt to clipboard
- **Copy Metadata** - Export full JSON metadata or human-readable params

#### Compare Mode
- **Side-by-Side View** - Compare two generations visually
- **Parameter Diff** - See exactly what changed (seed, prompt, preset, etc.)
- **Change Summary** - Quick overview of differences
- **View Modes** - SideBySide, Overlay, or DiffOnly

#### Workflow Search & Filter
- **Prompt Search** - Find images by prompt text (case-insensitive)
- **Seed Search** - Exact seed matching for finding variations
- **Preset Filter** - Filter by model/preset
- **Favorites Filter** - Show only favorited jobs
- **Date Range** - Filter by creation date
- **Combined Filters** - All filters work together (AND logic)

## Installation

### Option 1: MSIX Package (Recommended)

1. Download the `.msix` file from [Releases](https://github.com/mcp-tool-shop-org/next-gallery/releases)
2. Double-click to install
3. Launch from Start Menu

### Option 2: Build from Source

```powershell
git clone https://github.com/mcp-tool-shop-org/next-gallery
cd next-gallery
dotnet build Gallery.App
```

## Architecture

NextGallery follows Clean Architecture principles:

```
NextGallery.sln
├── Gallery.App/           # WinUI 3 desktop application
├── Gallery.Application/   # Use cases and business logic
├── Gallery.Domain/        # Core entities and interfaces
├── Gallery.Infrastructure/# File system, metadata parsing
├── Gallery.Tests/         # Unit and integration tests
└── Contracts/             # Shared contracts and DTOs
```

## Integration with CodeComfy

NextGallery is designed to work with the [CodeComfy VS Code extension](https://github.com/mcp-tool-shop-org/codecomfy-vscode) for a seamless ComfyUI workflow:

1. Generate images/videos in VS Code with CodeComfy
2. NextGallery automatically detects new outputs
3. Browse, organize, and manage your generations

## Requirements

- Windows 10 version 1809 or later
- .NET 9.0 Runtime
- Windows App SDK 1.4+

## Roadmap

See [ROADMAP_2026.md](ROADMAP_2026.md) for the full development roadmap including:
- Phase 0: CodeComfy Job Agency ✅
- Phase 1: Agency Foundation (multi-select, batch operations)
- Phase 2: Smart Metadata (AI prompt extraction, duplicate detection)
- Phase 3: Export Pipeline (ZIP, folder, cloud upload)
- Phase 4: UI/UX Polish (themes, accessibility)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.
