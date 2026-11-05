# Architecture

This document describes the architecture of the BricsCAD RC Automation plugin: the object model stored in the
drawing, the module structure, the main engineering rules, and the testing strategy.

The plugin is a .NET Framework 4.8 class library (C# 9, WPF) loaded into BricsCAD V25. The assembly is
`RcAutomation.dll`; the root namespace is `BricsCadRc`.

## 1. Design principles

- **The drawing is the database.** Every reinforcement object is a normal CAD entity (polyline, block
  reference, MLeader, MText) with its engineering data in XData. There is no external project file; copying,
  saving or sending a drawing carries all reinforcement data with it.
- **Pure rules separated from the CAD layer.** Geometry, BS 8666 shape and length rules, the section planner,
  layout rules and numbering rules are written as plain C# classes with no dependency on the BricsCAD API.
  CAD-facing engines read entities, call the rules and write the results back. The rule classes are compiled
  directly into the unit-test project.
- **Explicit links between objects.** Bars, labels, distributions and annotations reference each other by
  database handles stored in XData, always written and compared through one helper (`XLink`), and every link
  that is followed in both directions is checked from both sides (back-link check).
- **Additive record formats.** XData records are positional; new fields are only appended at the end, and
  readers tolerate older, shorter records. Old drawings keep working without migration, or are migrated lazily
  on first write.
- **Edit-aware behaviour.** Grips, COPY / MIRROR / ARRAY / PASTE, UNDO / REDO and multiple open drawings are
  handled by event-driven watchers so that the links and derived values (counts, lengths, labels) stay
  consistent after any edit.

## 2. Object model

| Object | CAD entity | XData application | Role |
|---|---|---|---|
| Bar (elevation) | `Polyline` (bar outline) or a block reference to its own block | `RC_SINGLE_BAR` | Definition of a bar mark: diameter, BS 8666 shape code, dimensions A–E, total length, mark (e.g. `H12-03`) |
| Bar label | `MLeader` | `RC_BAR_LABEL` | Label such as `24 H12-03`; the count is the sum over all distributions of the bar |
| Distribution | Block reference to an own block `RC_SLAB_BARS_nnn` | `RC_BAR_BLOCK` | Bars of one mark laid out in plan: count, spacing, cover, viewing direction, visibility of individual bars, end symbols |
| Distribution annotation | Block reference (distribution line + text) | `RC_BAR_ANNOT` | Distribution line, arrow and text such as `H12-03-200 B1` |
| Slab tag | on a distribution | `RC_AUTOREBAR_SLAB` | Handle of the slab outline the distribution was generated for |
| Beam zone | Closed polyline | `RC_BEAM` | Beam / thickening parameters (height, width, nib, links, bars, covers, options) |
| Beam detail objects | Polylines, lines, circles, texts on the detail layer | `RC_BEAM_DETAIL` | 1:25 detail of a beam / cantilever / pile cap: anchor, concrete lines, piles, title, frame, plan reference |
| Cantilever / protrusion zone | Closed polyline | `RC_CANT` | Cantilever or slab protrusion parameters |
| Pile cap | Closed polyline | `RC_PILECAP` | Pile cap dimensions taken from the GA drawing |
| Section title | `MText` "SECTION A-A" + Xrecord | `RC_SECTION` | Section definition: GUID, letter, cut line, viewing side, parameters, geometry fingerprint, list of parts |
| Section part | Entities of the section drawing and markers on plans | `RC_SECTION_PART` | Link to the section title, role and key of each part |

Links:

```
Bar ──[13] LabelHandle──► Label ──[1] bar handle──► Bar               (back-link checked)
Distribution ──[18] SourceBarHandle──► Bar
Distribution ──[11] AnnotHandle──► Annotation ──[16] SourceBlockHandle──► Distribution
Distribution ──RC_AUTOREBAR_SLAB──► slab outline
Beam zone ──RC_BEAM [6]──► slab outline;   beam distribution ──RC_AUTOREBAR_BEAM──► beam zone
Beam detail object ──RC_BEAM_DETAIL [2]──► beam zone
Section title ══Xrecord PART=handle…══► parts ──[1]──► title         (a part is confirmed in both directions)
```

### Link rules

- Handles are written only as DXF group code 1005 through `XLink.Write`, read through `XLink.Read` (which also
  accepts the legacy text form) and compared through `XLink.Same`.
- Before a label is modified, recounted or deleted, the back-link is checked: a copied bar without its own label
  must never change the label of the original.
- BricsCAD does not remap 1005 handles on COPY / MIRROR / ARRAY. The plugin records the clone map of each
  copy-like command (`CloneMap`) and re-links the clones afterwards (`CloneRemap`), so copied bars point to
  copied labels and copied distributions point to copied bars.

### XData records (examples)

- `RC_SINGLE_BAR`: [1] Mark, [2] Diameter, [3] ShapeCode, [4]–[6] A–C, [7] LayerCode, [8] Position, [9]–[10] D–E,
  [11] TotalLength, [12] LengthOverridden, [13] LabelHandle, [14] FormatVersion, [15] ShapeStd, …
  Shape parameters are positional: parameter *i* of the BS 8666 shape is stored in dimension field *i*; geometry
  reads dimensions by name. New shape parameters are only appended.
- `RC_BAR_BLOCK`: [1] Mark, [2] LayerCode, [3] Count, [4] Diameter, [5] Spacing, [6] Direction, [7] Position,
  [8] LengthA (length in plan), [9] BarsSpan, [10] Cover, [11] AnnotHandle, [12] ShapeCode, [13] SymbolSide,
  [14] SymbolDirection, [15] ViewingDirection, [16] ViewSegmentIndex, [17] SymbolType, [18] SourceBarHandle,
  … [21] VisibilityMode, [22] VisibleIndices, … [27] CountDisplay, [29] ShowSpacing, [30] IsLabelManual, …
- Writing goes through one writer per record type (for bars: `BarXData.Write` / `BarEntity.Write`), which
  preserves the tail of an existing record so that fields written by newer versions are not lost.

## 3. Modules

### `src/App`

- `PluginApp` — initialization: registration of overrules and watchers, ribbon, startup message.
- `RibbonBuilder` — ribbon tab with panels Setup, Reinforcement, Bars, Edit and Schedule.

### `src/Commands`

Thin command classes: user prompts, selection, dialogs and messages. They delegate the work to `src/Core`.
Examples: `RC_BAR`, `RC_DISTRIBUTION`, `RC_EDIT_BAR`, `RC_EDIT_DISTRIBUTION`, `RC_EDIT_LABEL`, `RC_GENERATE`,
`RC_PREPARE_GA`, `RC_IMPORT_ANALYSIS`, `RC_SECTION`, `RC_SECTION_CHECK`, `RC_SECTION_UPDATE`, `RC_LAYOUT`,
`RC_LAYOUT_TEXTS`, `RC_BBS`, `RC_BEAM`, `RC_PUNCHING_AUTO`, `RC_BAR_DIMS`, `RC_ZONES`, `RC_FIX_BAR_LABELS`.

### `src/Core` — engines (CAD layer)

- `SingleBarEngine`, `SingleBarBlock`, `BarEntity` — bars as polylines or blocks, labels, one parser of the bar
  record.
- `BarBlockEngine`, `AnnotationEngine` — distributions (bars in plan, end symbols, visibility of individual
  bars) and their annotations.
- `AutoRebarEngine` (partial classes) — automatic reinforcement of slabs: meshes, U-bars, nib bars, additional
  reinforcement from maps (`.Add`), diagonal corner bars (`.B3`), beams (`.Beam`, `.BeamDetail`), cantilevers
  (`.Cantilever`), pile caps (`.PileCap`), sections (`.Section`).
- `GaImportEngine`, `CantileverEngine`, `PileCapEngine`, `NibDetector` — conversion of a GA drawing into a
  reinforcement drawing: slab outlines, nibs, beam zones, cantilevers, protrusions and pile caps.
- `SectionStore`, `SectionGuard`, `SectionUpdater` — persistence, staleness check and update of sections.
- `LayoutEngine`, `PlotSetupEngine`, `RcTemplateEngine` — sheet layouts, sheet texts, plot settings, template.
- `BbsDrawingReader`, `BbsXlsGenerator`, `BbsNumberingCheck` — bar bending schedule from the drawing.
- `PositionCounter`, `PositionReconciler` — mark numbering and the "one mark = one shape" rule.

### `src/Core` — reactions to edits (event-driven)

- `BarGeometryWatcher` — after a bar is stretched by grips, recomputes its data, renumbers it if needed and
  propagates the change to its distributions and label.
- `BarCopyWatcher`, `CloneMap`, `CloneRemap` — re-linking after copy-like commands.
- `RcMLeaderOverrule`, `AnnotMoveOverrule`, `SingleBarGripOverrule` — grips and moves of labels, annotations
  and bars; label texts are regenerated from the bar data so that they cannot be edited as free text.
- `DocumentWatch` — per-document subscription for multiple open drawings.

BricsCAD-specific constraints that the watchers respect: no database writes during grip preview (changes are
applied at `CommandEnded`), UNDO / REDO are skipped by the handlers, the plugin's own changes do not re-trigger
the handlers, and no geometry state is cached between commands.

### Pure rule classes (no CAD dependency)

| Area | Classes |
|---|---|
| Bar shapes and lengths (BS 8666) | `BarShape`, `ShapeCodeLibrary`, `BarGeometryBuilder`, `BarFrameRules`, `BarLengthRules`, `BarDimensionRules`, `BarShapeValidation`, `BarRecordRules`, `LegacyBarGeometry` |
| Slab geometry | `RectilinearPolygons`, `OutlineAxisRules`, `NibLoopBuilder`, `NibStepRules`, `TerraceRules` |
| Beams, cantilevers, details | `BeamGeometry`, `BeamDetailRules`, `CantileverRules`, `PhDetailRules` |
| Additional reinforcement and laps | `AddReinfPlanner`, `TopLapSearch` |
| Sections | `SectionPlanner`, `SectionFingerprint`, `SectionRecord`, `SectionLabelCarry` |
| Labels, numbering, sheets, reports | `BarLabelRules`, `IdenticalBarText`, `BbsCountRules`, `LayoutRules`, `PlotLabelRules`, `PunchingReport`, `MTextParagraphs` |

## 4. Engineering rules (summary)

### Bar shapes — BS 8666

- Shape codes from BS 8666 with their parameters (A–E), bend radii and minimum dimensions. Total (cutting)
  length is computed per shape code from the dimensions, the diameter and the standard bend radius, rounded up
  to 25 mm, and the same function is used by the dialogs, the schedule and the counters.
- Outer dimensions are used, as in the standard; outline geometry follows the shape drawings of the standard,
  including angled legs and rings.
- Validation reports dimensions below the minimum lengths of the standard as warnings.

### Numbering

- Reserved marks for edge U-bars; automatic numbering starts after them; separate series for top
  reinforcement and for special bars.
- One mark = one bar shape. A bar whose dimensions change while its mark is used by a different bar receives the
  mark of an identical existing bar or a new mark (`PositionReconciler`).
- Before the schedule is produced, the drawing is checked for gaps in numbering, identical bars under different
  marks and different bars under one mark.

### Automatic slab reinforcement

- Meshes are laid out in strips derived from the rectilinear decomposition of the slab outline, with nominal
  spacing in labels and an upper limit on the actual spacing.
- Laps, maximum bar lengths and the positions of laps relative to piles are planned per bar line
  (`TopLapSearch`, with a time budget for very large slabs).
- Edge U-bars, nib bars, diagonal corner bars at re-entrant corners and additional reinforcement from the
  analysis maps (`AddReinfPlanner`): zones are merged, bars get anchorage beyond the demand contour, lengths
  are rounded, and distributions near a slab edge are placed at a fixed distance from the edge.
- Beam zones cut the mesh; zones that represent only a local corner thickening do not.

### Beams, cantilevers and pile caps

- Beams: links, bottom / top bars, side bars, U-bars at beam ends and nib bars, with configurable covers and
  options stored in the beam record.
- Cantilevers and protrusions: U-bars and links planned from the cantilever geometry.
- Pile caps: U-bars in both directions plus bars at the pile axis, drawn as a 1:25 detail.
- Identical elements share one detail with a member count in the title ("DETAIL '2' 5No."); the sheets add a
  note that the schedule quantities cover all members.

### Sections

- `RC_SECTION` cuts through the actual slab geometry and the distributions crossing the cut line.
  `SectionPlanner` (pure logic) produces segments, bar dots, bars in view, piles and dimensions.
- Each section stores a fingerprint of everything it was built from (outline, distributions, beam parameters).
  `RC_SECTION_CHECK` recomputes the fingerprints and marks outdated sections with a revision cloud; outputs
  (e.g. the schedule) warn before using outdated sections; `RC_SECTION_UPDATE` rebuilds them in place.

## 5. Testing

### Unit tests (`tests/`)

NUnit tests (.NET 8) compiled directly against the pure rule classes listed above, without BricsCAD. They cover
bar shape geometry (including frozen reference outlines from earlier geometry versions), BS 8666 lengths and
validation, section planning (golden results), beam / cantilever / terrace / pile-cap rules, polygon
decomposition, layout and label rules, and report parsing.

```
dotnet test tests/BricsCadRc.Tests.csproj
```

### Smoke tests in BricsCAD (`tools/BrxSmokeTests`)

PowerShell runners start BricsCAD in automation mode with a `.scr` script that loads the plugin and a test
assembly; the tests drive commands end-to-end through reflection and write PASS / FAIL reports. They need
BricsCAD V25 and test drawings (not included).

### Regression with expected diffs (`tools/RegressionDump`)

`run-regression.ps1` executes the same scenario three times in BricsCAD: with a baseline build and with the
current build (polyline and block bar formats). `RegressionDump` writes a textual dump of the drawing after each
run. The multiset difference between the baseline dump and the current dump must equal **exactly** the union of
the files in `tools/RegressionDump/expected-diffs/{polyline,block}`; each file documents one intended change.
Unexpected differences fail the run as blockers, and expected differences that do not appear are reported as
missing. The expected-diff files are maintained by hand: proposed differences are written to a separate output
folder and never directly into `expected-diffs`.

## 6. Development workflow

```text
requirements → planning → AI-assisted implementation → code review → automated testing → engineering validation
```

- Requirements and engineering rules are defined by the developer.
- A planning agent prepares an implementation plan (files, approach, risks, test steps); an implementation
  agent applies it and builds the project; an independent read-only reviewer reviews plans and diffs
  (`.claude/agents`, `.codex/agents`, `scripts/recenzja-gpt.ps1`).
- Every review remark is verified against the code before it is applied.
- Unit tests, smoke tests and the regression harness run before changes are accepted; the final validation is
  done by the developer on real drawings in BricsCAD.
