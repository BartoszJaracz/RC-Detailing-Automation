<#
.SYNOPSIS
  Independent code review with Codex CLI - read-only.

.DESCRIPTION
  Runs "codex exec" in a read-only sandbox with the instructions from scripts\recenzent-gpt.md.
  Saves the result to %LOCALAPPDATA%\BricsCadRc\recenzje\ and prints it.
  Requires: Codex CLI (npm i -g @openai/codex), logged in (codex login).
  Parameters: -Plan <file> (review a plan), -Baza <git ref> (diff base), -Uwagi <context>, -Model, -Rozumowanie (reasoning effort).

.EXAMPLE
  .\scripts\recenzja-gpt.ps1                       # review uncommitted changes (git diff HEAD)
  .\scripts\recenzja-gpt.ps1 -Baza origin/main     # review changes against origin/main
  .\scripts\recenzja-gpt.ps1 -Plan .\notes\plan.md   # review a plan (example path)
  .\scripts\recenzja-gpt.ps1 -Model gpt-5.6-sol -Rozumowanie medium  # different model (list: ~\.codex\models_cache.json)
#>
param(
    [string]$Plan,
    [string]$Baza = "HEAD",
    [string]$Uwagi,
    [string]$Model = "gpt-6-astra",
    [ValidateSet("low", "medium", "high", "xhigh")]
    [string]$Rozumowanie = "high"
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = $utf8
[Console]::OutputEncoding = $utf8

if (-not (Get-Command codex -ErrorAction SilentlyContinue)) {
    throw "Codex CLI not found. Install: npm i -g @openai/codex, then: codex login"
}

$instrukcja = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'recenzent-gpt.md'), $utf8)

if ($Plan) {
    $sciezka = (Resolve-Path $Plan).Path
    $zadanie = "MODE: PLAN. Review the plan in file '$sciezka'. Relate it to the current code in the repository."
} else {
    $zadanie = "MODE: DIFF. Review the changes against '$Baza': run 'git diff $Baza' and 'git status --short'; " +
               "read new, untracked files (??) in full. Skip the bin, obj and tools folders."
}
if ($Uwagi) { $zadanie += "`nContext from the team: $Uwagi" }

$prompt = $instrukcja + "`n`n## Task`n" + $zadanie

$dir = Join-Path $env:LOCALAPPDATA 'BricsCadRc\recenzje'
New-Item -ItemType Directory -Force $dir | Out-Null
$stamp = "{0:yyyyMMdd-HHmmss}" -f (Get-Date)
$out = Join-Path $dir "recenzja-$stamp.md"
$log = Join-Path $dir "recenzja-$stamp.log"

$codexArgs = @('exec', '-s', 'read-only', '-C', $repo, '--ephemeral', '--color', 'never', '-o', $out)
$codexArgs += @('-m', $Model, '-c', "model_reasoning_effort=$Rozumowanie")
$codexArgs += '-'

Write-Host "[Review] Start ($(if ($Plan) { 'plan' } else { "diff against $Baza" }))..."
# Codex activity (commands, file reads) goes to the log; only the result is printed.
$ErrorActionPreference = 'Continue'
$prompt | & codex @codexArgs *> $log
$kod = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($kod -ne 0 -or -not (Test-Path $out)) {
    Get-Content $log -Tail 15
    throw "[Review] Codex failed (exit code $kod). Log: $log"
}

Write-Host "[Review] Result: $out"
Write-Host ""
[IO.File]::ReadAllText($out, $utf8)
