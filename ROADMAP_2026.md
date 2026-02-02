# Next-Gallery Roadmap to Excellence (2026)

**Version:** 0.1.0 → 1.0.0
**Target:** Q2 2026
**Philosophy:** Bold moves, test often, ship fast

---

## EXECUTIVE SUMMARY

Next-Gallery has excellent **inspection** capabilities but lacks **agency** - the ability to act on media beyond viewing. This roadmap transforms it from a passive browser into an active media command center.

### Current State (v0.1.0)
- ✅ High-performance virtualized grid
- ✅ Full-text search with filters
- ✅ Temporal grouping (day/month)
- ✅ Favorites and ratings
- ✅ Quick preview overlay
- ✅ CodeComfy workspace integration
- ✅ Background thumbnail generation
- ❌ No batch operations
- ❌ No delete/move/copy
- ❌ No export functionality
- ❌ No AI metadata extraction
- ❌ No duplicate detection

### 2026 Industry Features (Research-Driven)
Based on analysis of SmartGallery, ComfyUI-Gallery, and industry trends:

| Feature | SmartGallery | ComfyUI-Gallery | Next-Gallery Target |
|---------|--------------|-----------------|---------------------|
| Compare Mode | ✅ Synchronized zoom/pan | ❌ | ✅ Diff table + visual |
| Metadata Copy | ❌ | ✅ Clipboard | ✅ Smart copy |
| Workflow Search | ✅ Model/sampler filters | ❌ | ✅ Full workflow query |
| Batch ZIP Export | ❌ | ✅ | ✅ With metadata |
| Job Management | ❌ | ❌ | ✅ Delete/re-run/clone |
| Parameter Diff | ✅ Diff table | ❌ | ✅ Side-by-side |

### Target State (v1.0.0)
- ✅ Everything above, plus:
- ✅ **Full Agency** - Delete, move, copy, rename
- ✅ **Batch Operations** - Multi-select, bulk actions
- ✅ **Smart Collections** - AI-powered auto-organization
- ✅ **Prompt Extraction** - Read ComfyUI/A1111 metadata
- ✅ **Duplicate Detection** - Perceptual hashing
- ✅ **Export Pipeline** - ZIP, folder, cloud upload
- ✅ **Dark/Light Theme** - System-aware theming
- ✅ **Keyboard-First UX** - Full vim-style navigation
- ✅ **Compare Mode** - Side-by-side with param diff (2026)
- ✅ **Job Management** - Delete, re-run, clone jobs (2026)
- ✅ **Metadata Clipboard** - One-click copy prompt/params (2026)

---

## PHASE 0: CODECOMFY JOB AGENCY (Week 1) - NEW 2026

### 0.1 Job Management Commands
**Priority: CRITICAL** - Core agency for AI workflow management

```csharp
// CodeComfyViewModel job action commands
public interface IJobActions
{
    Task DeleteJobAsync(JobRow job);           // Remove job + files
    Task<JobRow> RerunJobAsync(JobRow job);    // Clone params, new job
    Task OpenJobFolderAsync(JobRow job);       // Explorer to job files
    Task CopyMetadataAsync(JobRow job);        // Prompt/params to clipboard
}
```

**Deliverables:**
- [x] `DeleteJobCommand` - Remove job from index + optional file deletion
- [x] `RerunJobCommand` - Clone parameters for new generation
- [x] `OpenJobFilesCommand` - Open job output folder in Explorer
- [x] `CopyPromptCommand` - Copy prompt text to clipboard
- [x] `CopyFullMetadataCommand` - Copy JSON params to clipboard

### 0.2 Compare Mode (2026 Industry Standard)
**Priority: HIGH** - Feature parity with SmartGallery

```csharp
public record CompareSession(
    JobRow Left,
    JobRow Right,
    CompareViewMode ViewMode);  // SideBySide, Overlay, Diff

public record ParameterDiff(
    string Parameter,
    string? LeftValue,
    string? RightValue,
    bool IsDifferent);
```

**Deliverables:**
- [x] Compare mode toggle (StartCompareCommand)
- [x] Side-by-side image view (CompareViewMode.SideBySide)
- [x] Parameter diff table (ParameterDiffs collection)
- [x] "What changed" summary (ChangeSummary property)
- [x] Overlay blend mode (OverlayBlendRatio property)
- [x] Swap left/right (SwapCompareCommand)
- [x] Cycle view modes (CycleCompareViewModeCommand)

### 0.3 Workflow Search & Filter
**Priority: HIGH** - Find images by generation parameters

```csharp
public record WorkflowQuery(
    string? PromptContains,
    string? NegativePromptContains,
    string? Model,
    string? Sampler,
    IntRange? Steps,
    DoubleRange? CfgScale,
    long? Seed);
```

**Deliverables:**
- [x] Workflow filter panel (ToggleFilterPanelCommand)
- [x] Preset dropdown (AvailablePresets auto-populated)
- [x] Prompt text search (PromptSearchText)
- [x] Seed search - exact match (SeedSearchText)
- [x] Combined filter logic - AND (WorkflowQuery.Matches)
- [x] Filter summary display (FilterSummary property)
- [x] Quick filter by selected preset (FilterBySelectedPresetCommand)
- [x] Clear all filters (ClearFiltersCommand)

---

## PHASE 1: AGENCY FOUNDATION (Week 1-2)

### 1.1 Multi-Select Infrastructure
**Priority: CRITICAL**

```csharp
// SelectionService additions
public interface ISelectionService
{
    IReadOnlyList<MediaItem> SelectedItems { get; }
    bool IsMultiSelectMode { get; }
    void ToggleSelection(MediaItem item);
    void SelectRange(MediaItem start, MediaItem end);
    void SelectAll();
    void ClearSelection();
    event EventHandler<SelectionChangedEventArgs> SelectionChanged;
}
```

**Deliverables:**
- [ ] `SelectionService` multi-select support
- [ ] Shift+Click range selection
- [ ] Ctrl+Click toggle selection
- [ ] Ctrl+A select all
- [ ] Selection count badge in header
- [ ] Visual selection indicator on tiles

### 1.2 Core Actions (CRUD)
**Priority: CRITICAL**

```csharp
public interface IMediaActionService
{
    Task<bool> DeleteAsync(IEnumerable<MediaItem> items, bool moveToTrash = true);
    Task<bool> MoveAsync(IEnumerable<MediaItem> items, string targetFolder);
    Task<bool> CopyAsync(IEnumerable<MediaItem> items, string targetFolder);
    Task<bool> RenameAsync(MediaItem item, string newName);
}
```

**Deliverables:**
- [ ] `MediaActionService` implementation
- [ ] Delete command with confirmation dialog
- [ ] Move to folder dialog
- [ ] Copy to folder dialog
- [ ] Rename inline editing
- [ ] Undo stack for reversible actions

### 1.3 Keyboard Shortcuts
**Priority: HIGH**

| Key | Action |
|-----|--------|
| `Delete` / `Backspace` | Delete selected |
| `Ctrl+C` | Copy to clipboard |
| `Ctrl+X` | Cut (mark for move) |
| `Ctrl+V` | Paste (move/copy) |
| `F2` | Rename |
| `Ctrl+A` | Select all |
| `Escape` | Clear selection |
| `Ctrl+Shift+A` | Invert selection |

**Deliverables:**
- [ ] Global keyboard handler
- [ ] Action command bindings
- [ ] Shortcut hints in context menu

---

## PHASE 2: BATCH OPERATIONS (Week 2-3)

### 2.1 Batch Action Bar
**Priority: HIGH**

When multiple items selected, show action bar:
```
┌─────────────────────────────────────────────────────────────┐
│ 47 selected │ ★ Set Rating │ ♥ Favorite │ 📁 Move │ 🗑 Delete │
└─────────────────────────────────────────────────────────────┘
```

**Deliverables:**
- [ ] Floating action bar UI
- [ ] Batch rating assignment
- [ ] Batch favorite toggle
- [ ] Batch move/copy
- [ ] Batch delete with progress

### 2.2 Progress & Feedback
**Priority: MEDIUM**

```csharp
public record BatchProgress(
    int Completed,
    int Total,
    string CurrentItem,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining);
```

**Deliverables:**
- [ ] Progress dialog for long operations
- [ ] Cancel support
- [ ] Error summary with retry option
- [ ] Success toast notifications

---

## PHASE 3: SMART METADATA (Week 3-4)

### 3.1 AI Prompt Extraction
**Priority: HIGH**

Extract generation parameters from:
- PNG iTXt chunks (ComfyUI, A1111, Midjourney)
- EXIF UserComment (Stable Diffusion)
- JSON sidecar files

```csharp
public record GenerationMetadata(
    string? Prompt,
    string? NegativePrompt,
    string? Model,
    string? Sampler,
    int? Steps,
    double? CfgScale,
    long? Seed,
    string? LoRAs,
    Dictionary<string, string> RawParameters);
```

**Deliverables:**
- [ ] `IMetadataExtractor` interface
- [ ] PNG chunk reader (ComfyUI format)
- [ ] A1111 metadata parser
- [ ] Inspector panel: Generation info section
- [ ] Search by prompt text

### 3.2 Duplicate Detection
**Priority: MEDIUM**

Use perceptual hashing (pHash/dHash):
```csharp
public interface IDuplicateDetector
{
    Task<ulong> ComputeHashAsync(string imagePath);
    Task<IEnumerable<DuplicateGroup>> FindDuplicatesAsync(int hammingThreshold = 5);
}
```

**Deliverables:**
- [ ] `PerceptualHashService` implementation
- [ ] Database column for image hash
- [ ] "Find Duplicates" command
- [ ] Duplicate review UI (side-by-side)
- [ ] Keep/delete actions per duplicate

### 3.3 Smart Tags
**Priority: LOW**

Auto-generated tags from prompts:
- Character names
- Art styles
- Quality modifiers
- NSFW detection (local, no cloud)

**Deliverables:**
- [ ] Tag extraction from prompts
- [ ] Tag database schema
- [ ] Tag filter sidebar
- [ ] Tag editing UI

---

## PHASE 4: EXPORT PIPELINE (Week 4-5)

### 4.1 Export Wizard
**Priority: HIGH**

```csharp
public record ExportOptions(
    ExportFormat Format,        // ZIP, Folder, Individual
    ExportNaming Naming,        // Original, Sequential, DateBased
    bool IncludeMetadata,       // Sidecar JSON
    ImageResizeOptions? Resize, // Optional resizing
    string? Watermark);         // Optional watermark
```

**Deliverables:**
- [ ] Export wizard dialog
- [ ] ZIP archive creation
- [ ] Folder export with structure
- [ ] Metadata sidecar option
- [ ] Optional resize on export

### 4.2 Quick Export
**Priority: MEDIUM**

One-click exports:
- [ ] "Copy to Downloads" (Ctrl+Shift+D)
- [ ] "Export Selection" (Ctrl+E)
- [ ] Recent export locations

### 4.3 Share Integration
**Priority: LOW**

- [ ] Windows Share contract
- [ ] Copy image to clipboard
- [ ] Direct upload to Imgur/Catbox (optional)

---

## PHASE 5: UI/UX POLISH (Week 5-6)

### 5.1 Theme System
**Priority: HIGH**

```csharp
public enum AppTheme { System, Light, Dark, OLED }
```

**Deliverables:**
- [ ] Light theme with proper contrast
- [ ] OLED theme (true black)
- [ ] System theme detection
- [ ] Theme toggle in settings
- [ ] Per-session theme persistence

### 5.2 Grid Customization
**Priority: MEDIUM**

- [ ] Adjustable thumbnail size (80-200px)
- [ ] Compact/comfortable/spacious density
- [ ] Column count slider (2-12)
- [ ] List view alternative

### 5.3 Inspector Enhancements
**Priority: MEDIUM**

- [ ] Collapsible sections
- [ ] Generation parameters display
- [ ] Histogram (optional)
- [ ] Quick edit (crop, rotate)

### 5.4 Accessibility
**Priority: MEDIUM**

- [ ] High contrast mode support
- [ ] Screen reader announcements
- [ ] Keyboard focus indicators
- [ ] Reduced motion option

---

## PHASE 6: PERFORMANCE & RELIABILITY (Week 6-7)

### 6.1 Incremental Scanning
**Priority: HIGH**

```csharp
public interface IFileWatcher
{
    void Start(IEnumerable<string> folders);
    event EventHandler<FileChangedEventArgs> FileChanged;
}
```

**Deliverables:**
- [ ] `FileSystemWatcher` integration
- [ ] Real-time index updates
- [ ] Debounced change batching
- [ ] Scan queue with priorities

### 6.2 Database Optimization
**Priority: MEDIUM**

- [ ] Index on common query columns
- [ ] WAL mode for concurrent access
- [ ] Query result caching
- [ ] Lazy loading for large collections

### 6.3 Memory Management
**Priority: MEDIUM**

- [ ] Thumbnail memory pooling
- [ ] LRU cache for loaded images
- [ ] Explicit GC hints after large ops
- [ ] Memory usage monitoring

---

## PHASE 7: TESTING & QUALITY (Continuous)

### 7.1 Unit Tests
**Target: 80% coverage**

| Layer | Current | Target |
|-------|---------|--------|
| Domain | 60% | 95% |
| Application | 40% | 90% |
| Infrastructure | 20% | 80% |
| ViewModels | 10% | 70% |

### 7.2 Integration Tests

- [ ] SQLite store tests
- [ ] Thumbnail generation tests
- [ ] Metadata extraction tests
- [ ] Export pipeline tests

### 7.3 UI Tests

- [ ] Appium/WinAppDriver setup
- [ ] Critical path automation
- [ ] Screenshot regression tests

---

## RELEASE MILESTONES

### v0.2.0 - "Agency" (Week 2)
- Multi-select
- Delete/Move/Copy
- Keyboard shortcuts

### v0.3.0 - "Batch" (Week 3)
- Batch operations bar
- Progress dialogs
- Undo stack

### v0.4.0 - "Smart" (Week 4)
- Prompt extraction
- Duplicate detection
- Search by prompt

### v0.5.0 - "Export" (Week 5)
- Export wizard
- ZIP/folder export
- Quick export shortcuts

### v0.6.0 - "Polish" (Week 6)
- Theme system
- Grid customization
- Accessibility

### v1.0.0 - "Excellence" (Week 7)
- Performance tuning
- Full test coverage
- Documentation
- Microsoft Store ready

---

## TECHNICAL DEBT CLEANUP

### Immediate
- [ ] Fix PowerShell variable expansion in build scripts
- [ ] Update to .NET 10 when stable
- [ ] Remove unused XAML resources
- [ ] Consolidate duplicate converters

### Short-term
- [ ] Extract magic numbers to constants
- [ ] Add XML documentation to public APIs
- [ ] Implement `IDisposable` where needed
- [ ] Add cancellation token support throughout

### Long-term
- [ ] Consider source generators for MVVM
- [ ] Evaluate Native AOT readiness
- [ ] Cross-platform abstraction layer
- [ ] Plugin architecture

---

## SUCCESS METRICS

| Metric | Current | Target |
|--------|---------|--------|
| Startup time | ~2s | <1s |
| Grid scroll FPS | 30 | 60 |
| Memory (10K items) | 400MB | 200MB |
| Test coverage | 30% | 80% |
| GitHub stars | 0 | 100+ |
| User satisfaction | N/A | 4.5/5 |

---

## APPENDIX: ARCHITECTURE CHANGES

### New Services
```
Gallery.Application/
├── Services/
│   ├── IMediaActionService.cs      # CRUD operations
│   ├── IExportService.cs           # Export pipeline
│   ├── IDuplicateDetector.cs       # Perceptual hashing
│   └── IMetadataExtractor.cs       # AI metadata

Gallery.Infrastructure/
├── Services/
│   ├── MediaActionService.cs
│   ├── ExportService.cs
│   ├── PerceptualHashService.cs
│   └── PngMetadataExtractor.cs
```

### Database Schema Changes
```sql
-- Add to MediaItems
ALTER TABLE MediaItems ADD COLUMN PerceptualHash INTEGER;
ALTER TABLE MediaItems ADD COLUMN Prompt TEXT;
ALTER TABLE MediaItems ADD COLUMN NegativePrompt TEXT;
ALTER TABLE MediaItems ADD COLUMN Model TEXT;
ALTER TABLE MediaItems ADD COLUMN Seed INTEGER;

-- New index for duplicate detection
CREATE INDEX IX_MediaItems_PerceptualHash ON MediaItems(PerceptualHash);

-- New index for prompt search
CREATE INDEX IX_MediaItems_Prompt ON MediaItems(Prompt);
```

---

*Roadmap created: 2026-02-01*
*Last updated: 2026-02-02*
*Author: Claude + mcp-tool-shop*

---

## CHANGELOG

### 2026-02-02
- ✅ **Phase 0 Complete** - All CodeComfy Job Agency features implemented
  - Job actions: Delete, Open, Copy Prompt, Copy Metadata, Copy Params
  - Compare Mode: Side-by-side, Overlay, DiffOnly with parameter diff
  - Workflow Search: Prompt, Seed, Preset, Favorites filtering
- Added 30 new tests (165 total)
- Updated README with 2026 features
- Added v0.3.0 release notes
