# BricsCadRc — instructions for Claude Code

A .NET plugin (net48, C# 9, WPF) for BricsCAD V25: detailing of reinforced-concrete slabs — bars,
distributions, labels, BS 8666 bar bending schedule, automatic slab reinforcement (AutoRebar), punching tags.

**Before changing anything, read [ARCHITECTURE.md](ARCHITECTURE.md).** The engineering rules (numbering,
AutoRebar rules) are requirements set by the engineer — do not break or "simplify" them without approval.

**Baseline:** results on the drawing are compared with a baseline build by the regression harness
(`tools/RegressionDump`, expected differences in `tools/RegressionDump/expected-diffs`). A change of the drawn
result that is not on the expected-diff list is a blocker unless it was explicitly approved.

## Communication
- The engineer tests in BricsCAD. After every change report: what changed, which files, and a short list of
  test steps to perform in BricsCAD.

## Agent team
- `planista-recenzent` (planner / reviewer) — prepares the implementation plan. Does not edit files. Reviews the
  diff only when the independent reviewer is unavailable.
- `implementator` (implementer) — applies the plan and the review fixes, builds the project.
- **Independent reviewer** (Codex CLI, read-only) — reviews plans and diffs:
  `scripts/recenzja-gpt.ps1` (instructions: `scripts/recenzent-gpt.md`), command `/recenzja-gpt`.
  Every remark is verified in the code before it is passed to the implementer — never accepted blindly.

Task flow: requirements → plan → plan review (larger changes) → plan approved by the engineer →
implementation + build → diff review → verification of remarks → fixes (max. 3 rounds) →
automated tests → test instructions → engineering validation in BricsCAD.
When the plan contradicts ARCHITECTURE.md or an engineering requirement is unclear — ask, do not guess.

## Build
```
dotnet build src/BricsCadRc.csproj -c Release
```
- Do not use `build.bat` (it ends with `pause`).
- Output: `bin\RcAutomation.dll`. If the build cannot overwrite the DLL, BricsCAD has it loaded — ask for
  BricsCAD to be closed, do not work around it.
- Tests in a real BricsCAD (scripted, no GUI): `tools\BrxSmokeTests\run-planc-*.ps1` — PASS / FAIL report files.
- Unit tests (pure geometry, no BricsCAD API): `dotnet test tests/BricsCadRc.Tests.csproj`

## Git
- **Do not commit and do not push.** Commits are made by the developer after testing; you may propose a
  commit message.

## Code style
- Existing code comments and command-line messages are in Polish (messages use prefixes such as
  `[AutoRebar]`, `[RC AUTO]`).
- Keep the line endings of each file (most files use CRLF).
- Instead of empty `catch {}` use `Log.Error("Class.Method", ex)` (log: `%LOCALAPPDATA%\RcAutomation\log.txt`).
- Numbers to / from text: `CultureInfo.InvariantCulture` / `NumberParser`.
- Links between objects in XData only through `XLink.Write` / `XLink.Read` / `XLink.Same`.
- Position numbers only through `PositionCounter.NextAuto`.
