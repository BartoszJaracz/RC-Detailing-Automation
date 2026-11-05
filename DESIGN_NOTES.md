# Design Notes

This document summarises the main design decisions behind the BricsCAD RC Automation plugin: what the
application does, how it is structured, how the engineering logic is organised and how it is tested. It
complements [ARCHITECTURE.md](ARCHITECTURE.md), which documents the object model, the XData records and the
module list in more detail.

All examples in this document are synthetic.

## 1. Project Overview

**Problem.** Reinforcement detailing of RC slabs on piles is repetitive and error-prone: hundreds of bars have
to be drawn, numbered, labelled, cut in sections and listed in a bar bending schedule (BBS), and every later
change of the slab geometry has to be propagated by hand.

**Goal.** Automate the repetitive part of the detailing, while keeping every result as ordinary, editable CAD
geometry that the engineer reviews and corrects.

**Environment.** A .NET Framework 4.8 class library (C# 9, WPF dialogs) loaded into BricsCAD V25 on Windows.
The plugin works inside the drawing; it has no separate database or project file.

**Automated tasks.**

- conversion of a general arrangement (GA) drawing into a reinforcement drawing (slab outlines, nibs, beam
  zones, cantilevers, pile caps, sheet layout);
- generation of slab meshes, edge U-bars, nib bars, diagonal corner bars and additional reinforcement from
  imported reinforcement-demand maps;
- standard reinforcement and 1:25 details for beams, cantilevers and pile caps;
- cross-sections cut through the actual drawn geometry, with detection of outdated sections;
- bar numbering, labels, sheet layouts and a BS 8666 bar bending schedule generated from the drawing;
- consistency of links and derived values (counts, lengths, labels) after normal CAD editing.

## 2. Architecture and Design

The source code is organised in four folders under `src/`. The `src/Core` folder contains two logical layers
(CAD engines and pure rule classes), which gives five areas of responsibility:

| Area | Location | Responsibility |
|---|---|---|
| Application | `src/App` | Plugin start-up, registration of overrules and watchers, ribbon |
| Commands | `src/Commands` | Thin command classes: prompts, selection, dialogs, messages; no engineering logic |
| CAD engines | `src/Core` (engines, watchers) | Read entities and XData, call the rule classes, write entities back; react to edits |
| Pure rules | `src/Core` (rule classes) | Geometry, BS 8666 shapes and lengths, planning, numbering, layout rules — no BricsCAD API |
| Dialogs | `src/Dialogs` | WPF dialogs (XAML and code-built) |

**Separation of domain logic from the CAD interface.** The rule classes (for example `SectionPlanner`,
`BarGeometryBuilder`, `BarLengthRules`, `RectilinearPolygons`, `BeamGeometry`, `AddReinfPlanner`,
`TopLapSearch`, `LayoutRules`) take plain values, tuples and lists and return plain results. They do not
reference the BricsCAD assemblies; the unit-test project compiles these source files directly
(`tests/BricsCadRc.Tests.csproj`) and runs them on .NET 8 without BricsCAD.

**Data flow.**

```text
+-----------------------------+
| Entities + XData / Xrecords |
+-----------------------------+
               |  read (CAD engine)
               v
+-----------------------------+
| Plain input model           |
+-----------------------------+
               |
               v
+-----------------------------+
| Pure rule / planner         |   no BricsCAD API, unit-tested
+-----------------------------+
               |
               v
+-----------------------------+
| Plain result                |   points, bars, dots, labels, warnings
+-----------------------------+
               |  write (CAD engine)
               v
+-----------------------------+
| Entities + XData / Xrecords |
+-----------------------------+
```

**The drawing as the single source of truth.** Each reinforcement object (bar, distribution, label, section,
beam zone) is a normal CAD entity carrying its engineering data in XData; sections additionally keep their
definition in an Xrecord. Links between objects are stored as database handles (DXF group code 1005) and are
written, read and compared through one helper (`XLink`), which also accepts the older text form of a handle.
XData records are positional: new fields are appended at the end, readers tolerate shorter (older) records, and
the bar writer preserves the tail of an existing record, so drawings created by earlier versions remain
readable.

**Edit awareness.** Event-driven watchers and overrules (`BarGeometryWatcher`, `BarCopyWatcher` with
`CloneMap` / `CloneRemap`, `RcMLeaderOverrule`, `SingleBarGripOverrule`, `DocumentWatch`) keep links, counts
and labels consistent after grips, COPY / MIRROR / ARRAY, UNDO / REDO and with several drawings open.

## 3. Core Engineering Logic

The descriptions below are at the level of responsibilities and decisions, not a full algorithm
specification.

**Slab geometry.** Slab outlines are decomposed into rectangles (`RectilinearPolygons`); the decomposition
drives the layout of mesh strips and the subtraction of beam zones. Nib lines are closed into loops
(`NibLoopBuilder`) and step edges with a free end are handled by dedicated rules (`NibStepRules`). Small
deviations of nearly orthogonal outline edges are tolerated by a dedicated rule class (`OutlineAxisRules`).

**Planning and placement.** Generation is planned before anything is drawn:

- mesh bars are planned per strip with nominal spacing shown in the labels;
- lap positions along a bar line are searched against obstacles such as piles (`TopLapSearch`); the search
  has a time budget and falls back to a simpler plan with a warning when the budget is exceeded;
- additional reinforcement is planned from imported demand maps (`AddReinfPlanner`): demand zones are merged,
  bars are anchored beyond the demand contour and the diameter is chosen from the required area;
- beams, cantilevers and pile caps have their own rule classes (`BeamGeometry`, `CantileverRules`,
  `BeamDetailRules`), with parameters stored in the zone record and editable in a dialog.

**Bar shapes.** Bars use BS 8666 shape codes with dimensions A–E. One library (`ShapeCodeLibrary`,
`BarShape`) defines the parameters of each supported code, and one length function (`BarLengthRules`) is
used by the dialogs, the counters and the schedule, so the same bar always has the same cutting length.
`BarGeometryBuilder` produces the drawn outline, including bend arcs. Shape codes that are not implemented are
drawn as a straight bar, and the bar command informs the user.

**Sections.** `SectionPlanner` turns the cut line and the distributions crossing it into concrete segments,
bar dots, bars in view, piles, dimensions and labels. Each section stores a fingerprint of everything it was
built from (`SectionFingerprint`); a later check marks outdated sections, and outputs warn before using them.

**Ambiguous cases and manual detailing.** The plugin does not try to resolve every situation automatically:

- pile results from an imported punching-shear report that exceed the capacity, report a failure, require
  shear reinforcement or lie at re-entrant corners are not tagged automatically; they are marked
  "MANUAL DESIGN" on the drawing;
- beam zones can be drawn by hand on a dedicated layer when they cannot be detected from the GA drawing;
- geometric situations outside the implemented rules produce warnings instead of guessed reinforcement.

**Validation of input data.** Bar dimensions below the minimum lengths are reported as warnings
(`BarShapeValidation`); a bar whose cutting length cannot be determined is listed as "no length" instead of
stopping the command; before the schedule is produced, numbering is checked for gaps, identical bars under
different marks and different bars under one mark (`BbsNumberingCheck`).

## 4. Design Decisions and Trade-offs

| Decision | Rationale | Consequence |
|---|---|---|
| Pure rule classes without CAD dependencies | Engineering logic can be unit-tested quickly and deterministically | CAD engines need a small mapping layer between entities and plain models |
| Planning separated from drawing | Plans can be validated, tested and compared before any entity is created | Two representations of the same result (plan and entities) must be kept in sync |
| XData-based object metadata with append-only records; section definitions additionally in Xrecords | Drawings stay self-contained; old drawings keep working | Field positions are fixed; every reader must tolerate shorter records |
| One helper for handle links, back-link checks | Copied objects never modify the originals' labels | Extra work after copy-like commands (clone map and re-linking) |
| No database writes during grip preview; changes applied at the end of the command; UNDO / REDO skipped by handlers | Required for stable behaviour in BricsCAD | Some updates appear only after the command finishes |
| Section fingerprints instead of automatic rebuilds | The engineer decides when a section is regenerated | Outdated sections must be checked explicitly |
| Ambiguous cases left to the engineer | Avoids plausible-looking but wrong reinforcement | Some elements still need manual detailing |
| Time budget for the lap search | Large slabs cannot block the CAD session | In rare cases a simpler fallback plan is used, with a warning |
| Optional resources embedded only if present (`Condition="Exists(...)"`) | The project builds without proprietary templates | Without them, the related features show a message or need a user-selected file |

## 5. Testing Strategy

**Unit tests** (`tests/`, NUnit, .NET 8) run the pure rule classes without BricsCAD:
`dotnet test tests/BricsCadRc.Tests.csproj`. Last verified result for the current version: 1307 tests, all
passing (on Windows and on Linux). They cover bar shape geometry and lengths, validation, polygon
decomposition, nib and beam rules, section planning, layout and label rules, and report parsing. Geometry
tests use synthetic coordinates or coordinates translated to a local origin.

**Frozen geometry.** Reference outlines of earlier geometry versions are kept as frozen data, so changes to
the shape generator are visible as explicit test differences.

**Golden tests.** `SectionPlannerGoldenTests` dumps the complete result of the section planner for a set of
reference inputs (11 cases) and compares a SHA-256 of the dump with stored values. Before hashing, every
floating-point value in the dump (coordinates and dimensions in mm) is written on a 1e-6 mm grid with
invariant-culture formatting (trailing zeros removed, negative zero written as zero); texts, identifiers,
integers and flags are written unchanged. The purpose is to remove differences in the last bit of
floating-point results between platforms (for example between the math libraries used on Windows and Linux);
it does not hide real geometry changes — any change of a value by 1e-6 mm or more changes the hash, and
dedicated tests check that changes of 0.01 mm and 0.1 mm are detected and that no reference value lies close
to a rounding boundary. A full, unnormalised dump (`Dump`) is still used for exact comparisons within one test
run.

**Regression with expected differences** (`tools/RegressionDump`): the same scripted scenario is run in
BricsCAD with a baseline build and with the current build; the difference between the two drawing dumps must
equal exactly the set of intended changes listed in `tools/RegressionDump/expected-diffs`.

**Smoke tests** (`tools/BrxSmokeTests`): scripted BricsCAD runs that execute commands end-to-end and write
PASS / FAIL reports. They require BricsCAD V25 and test drawings that are not part of this repository.

## 6. Known Limitations

- Windows and BricsCAD V25 only; the project references the BricsCAD .NET assemblies from the default
  installation folder.
- Optional resources (BBS template, sheet template, plot style table) are not included; without them the
  related features show a message or require a user-selected file (see README, *Optional Resources*).
- Smoke tests and regression runs need BricsCAD and test drawings that are not published; only the unit tests
  run in a plain .NET environment.
- Not every BS 8666 shape code is implemented; unsupported codes are drawn as a straight bar (for example, the
  helix shape is not supported).
- Situations outside the implemented rules (see *Ambiguous cases* above) are flagged for manual detailing.
- Code comments and user messages are mostly in Polish; the public documentation is in English.
- Historical commits: only the most recent commit embeds the optional resources conditionally. Earlier commits
  that already reference them (from the introduction of the embedded BBS template onwards) reference them
  unconditionally and therefore do not build unless the resource files are present.

## 7. Future Improvements (potential, not implemented)

- A synthetic demo drawing and screenshots for the README.
- Translation of code comments and user messages to English.
- Continuous integration running the unit tests on Windows and Linux.
- Wider BS 8666 shape coverage and more geometry cases for automatic detection of beams and cantilevers.
- A synthetic test-drawing set that allows smoke tests to run outside the original environment.
