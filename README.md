# BricsCAD RC Automation Plugin

## Overview

A .NET plugin for BricsCAD V25 that automates the detailing of reinforced-concrete (RC) slabs on piles: it
places bars and bar distributions, labels them, generates standard reinforcement for slabs, beams, cantilevers
and pile caps, cuts real cross-sections through the drawing and produces a bar bending schedule (BBS) according
to BS 8666.

The plugin works directly on the CAD drawing. Every reinforcement object (bar, distribution, label, section,
beam zone) is an ordinary CAD entity that carries its engineering data in extended entity data (XData), so the
drawing itself remains the single source of truth.

This repository is a portfolio version of the project. Client data, proprietary templates and project drawings
are not included.

## Key Features

- **Bars and distributions** — bars defined by BS 8666 shape codes (dimensions A–E, total length, bend
  radii), distributions (count, spacing, cover, viewing direction) and linked labels that keep their counts in
  sync with the drawing.
- **Automatic slab reinforcement** — bottom and top meshes, U-bars along edges and nibs, additional
  reinforcement generated from imported reinforcement-demand maps, diagonal corner bars, lap and bar-length
  rules.
- **Beams, cantilevers and pile caps** — detection of thickenings, protrusions and pile caps from the general
  arrangement (GA) drawing, standard reinforcement layouts and 1:25 details, including repeated (typical)
  details.
- **Cross-sections** — real cuts through the drawn slab (`RC_SECTION`), with a fingerprint of the cut geometry
  that detects outdated sections (`RC_SECTION_CHECK`, `RC_SECTION_UPDATE`).
- **Bar bending schedule** — `.xls` BBS generated straight from the drawing, with numbering checks (gaps,
  duplicates, identical bars under different marks).
- **Position numbering rules** — one mark = one bar shape; automatic renumbering when a copied bar changes
  shape.
- **Drawing preparation and sheets** — conversion of a GA drawing into a reinforcement drawing, automatic sheet
  layouts and sheet title texts.
- **Edit-aware behaviour** — grips, copy/mirror/array, undo/redo and multi-document handling keep the links
  between bars, distributions and labels consistent.

## Workflow

```text
GA drawing ──► Prepare GA ──► Import analysis maps ──► Generate (mesh, U-bars, ADD, beams, pile caps)
          ──► Manual edits (RC_BAR / RC_EDIT_*) ──► Sections ──► Layouts ──► BBS
```

1. `RC_PREPARE_GA` turns the general arrangement into a reinforcement drawing (slab outlines, nibs, beam zones,
   cantilevers, pile caps, sheet template).
2. `RC_IMPORT_ANALYSIS` / `RC_IMPORT_MAP` bring in the reinforcement-demand maps from the structural analysis.
3. `RC_GENERATE` creates the reinforcement (Mesh, B1/B2/T1/T2, UB, B3, Add, Beam, Cantilever, PileCap).
4. The engineer reviews and edits the result with the bar / distribution / label commands.
5. `RC_SECTION` cuts sections; `RC_LAYOUT` builds the sheets; `RC_BBS` produces the schedule.

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for details. In short:

- `src/App` — plugin entry point, ribbon.
- `src/Commands` — CAD commands (user interaction, prompts, dialogs).
- `src/Core` — engines and rules. Pure rule classes (geometry, BS 8666 shapes and lengths, section planner,
  layout rules, numbering rules) have no dependency on the BricsCAD API and are unit-tested directly.
- `src/Dialogs` — WPF dialogs.
- `tests` — NUnit tests of the pure rule classes.
- `tools/BrxSmokeTests` — smoke tests executed inside a real BricsCAD instance via scripts.
- `tools/RegressionDump` — regression harness that dumps drawing state and compares it with a baseline using
  an explicit list of expected differences.

For design decisions and technical trade-offs, see [DESIGN_NOTES.md](DESIGN_NOTES.md).

## Testing

- **Unit tests** (`tests/`, NUnit, .NET 8): geometry of bar shapes, BS 8666 lengths and validation, section
  planning (including golden-result tests), beam / cantilever / pile-cap rules, layout rules, label and
  numbering rules. Run with `dotnet test tests/BricsCadRc.Tests.csproj`.
- **Smoke tests in BricsCAD** (`tools/BrxSmokeTests`): scripted runs of BricsCAD (`.scr` + PowerShell runners)
  that exercise commands end-to-end and write PASS / FAIL reports. They require BricsCAD V25 and test drawings,
  which are not part of this repository.
- **Regression with expected diffs** (`tools/RegressionDump`): the same scenario is executed with a baseline
  build and with the current build; the drawing dumps must differ by exactly the set of changes listed in
  `tools/RegressionDump/expected-diffs`. Any unexpected or missing difference fails the run.

## Optional Resources

The plugin can embed three optional resource files. They are proprietary and are not included in this
repository; the project builds and runs without them:

| File (in `src/Resources`) | Used by | Behaviour when missing |
| --- | --- | --- |
| `default-bbs.xls` | `RC_BBS` (BBS template) | a message is shown; a BBS template file can be selected instead |
| `rc-template.dxf.gz` | `RC_PREPARE_GA` (sheet template) | a message is shown; the drawing is prepared without sheet titles, layout and pile details |
| `demo-mono.ctb` | `RC_USTAW_WYDRUK` / sheet preparation (plot style table) | a message is shown; the table is not copied to BricsCAD (a table already installed there is used) |

To use your own resources, place the files in `src/Resources` before building — the project file embeds each
one only if it exists (`Condition="Exists(...)"`). The files are listed in `.gitignore`.
`tools/RcTemplate/build_template.py` builds a sheet template from your own DXF drawing.

## Technology

- C# 9, .NET Framework 4.8, WPF
- BricsCAD V25 .NET API (`BrxMgd`, `TD_Mgd`)
- NPOI (BBS `.xls` output)
- NUnit (.NET 8) for unit tests, PowerShell for test automation

## Demo

No demo drawings are included: project drawings and proprietary templates are not part of this repository.
Screenshots or a synthetic demo drawing may be added later.

## Project Context

The plugin automates repetitive RC detailing tasks for piled raft foundations of residential buildings.

The engineering rules implemented in the code are explicit and testable. The developer specified them for
implementation and validated the results; their default values reflect the detailing conventions used during
development and should be reviewed before use on other projects.

## My Role

- Defining the requirements and engineering rules (structural design background).
- Designing the object model, workflow and architecture.
- AI-assisted implementation: code was written with AI coding assistants under my direction, following the
  workflow *requirements → planning → AI-assisted implementation → code review → automated testing →
  engineering validation*.
- Reviewing changes, writing and maintaining tests, validating results on project drawings in BricsCAD.

## Disclaimer

This is a portfolio version of the project. It contains no client data, project drawings or proprietary
templates; organisation, client and project names have been replaced with neutral placeholders (for example
`DemoClient`, `DemoProject`). Optional resource files (sheet template, BBS template, plot style table) are not
included — see [Optional Resources](#optional-resources). Generated reinforcement must always be checked
by a qualified engineer.
BricsCAD is a trademark of Bricsys NV; this project is not affiliated with Bricsys.
