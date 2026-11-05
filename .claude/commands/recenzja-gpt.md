---
description: Independent review of changes (or of a plan) by Codex, followed by verification of the remarks
argument-hint: "[-Plan path] [-Baza ref] [-Uwagi \"context\"]"
---

1. Run (PowerShell, timeout 600000 ms):
   `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/recenzja-gpt.ps1 $ARGUMENTS`
2. Verify every remark in the code and in ARCHITECTURE.md — the reviewer can be wrong too.
   List rejected remarks with a one-sentence justification.
3. Show a table: remark · severity · decision (accepted / rejected) · what to fix.
4. Pass the accepted fixes to the `implementator` agent (max. 3 review rounds per task).
   Do not commit.
