<#
.SYNOPSIS
  Wybiera, ktora wtyczka zbrojenia BricsCAD V25 wczytuje sie przy starcie: RC Automation albo stara BricsCadRc.

.DESCRIPTION
  Obie wtyczki moga byc zainstalowane jednoczesnie, ale wczytana jest tylko jedna (maja te same komendy).
  Skrypt ustawia autoload w rejestrze (HKCU\Software\Bricsys\BricsCAD\V25x64\en_US\Applications):
  wybrana wtyczka LOADCTRLS=2 (start), druga LOADCTRLS=0 (nie wczytywac). Zmiana dziala od nastepnego
  uruchomienia BricsCAD. Bez parametrow pokazuje stan.

.EXAMPLE
  .\scripts\wybierz-wtyczke.ps1                  # stan
  .\scripts\wybierz-wtyczke.ps1 -Wtyczka RcAutomation
  .\scripts\wybierz-wtyczke.ps1 -Wtyczka BricsCadRc
#>
param(
    [ValidateSet("RcAutomation", "BricsCadRc")]
    [string]$Wtyczka
)

$ErrorActionPreference = 'Stop'
$apps = "HKCU:\Software\Bricsys\BricsCAD\V25x64\en_US\Applications"
$repo = Split-Path -Parent $PSScriptRoot

# Domyslne lokalizacje DLL (klucze sa tworzone, jesli ich brak)
$domyslne = @{
    "RcAutomation"  = Join-Path $repo "bin\RcAutomation.dll"
    "BricsCadRc" = "C:\Projects\BricsCadRc - origin\bin\BricsCadRc.dll"
}

function Pokaz {
    foreach ($n in "RcAutomation", "BricsCadRc") {
        $k = Join-Path $apps $n
        if (Test-Path $k) {
            $p = Get-ItemProperty $k
            $stan = if ($p.LOADCTRLS -eq 2) { "WCZYTYWANA przy starcie" } else { "wylaczona (LOADCTRLS=$($p.LOADCTRLS))" }
            Write-Host ("{0,-11} {1}  ->  {2}" -f $n, $stan, $p.LOADER)
        } else {
            Write-Host ("{0,-11} brak wpisu w rejestrze" -f $n)
        }
    }
}

if (-not $Wtyczka) { Pokaz; return }

# Najpierw sprawdz, czy wybrana wtyczka istnieje - inaczej po restarcie nie wczytalaby sie zadna
$kWyb = Join-Path $apps $Wtyczka
$dllWyb = if (Test-Path $kWyb) { (Get-ItemProperty $kWyb).LOADER } else { $domyslne[$Wtyczka] }
if (-not (Test-Path $dllWyb)) { throw "Brak pliku $dllWyb - zbuduj wtyczke albo popraw LOADER. Autoload bez zmian." }

foreach ($n in "RcAutomation", "BricsCadRc") {
    $k = Join-Path $apps $n
    if (-not (Test-Path $k)) {
        New-Item -Path $k -Force | Out-Null
        Set-ItemProperty $k -Name LOADER  -Value $domyslne[$n]
        Set-ItemProperty $k -Name MANAGED -Value 1 -Type DWord
        Set-ItemProperty $k -Name DESCRIPTION -Value $n
    }
    $ctrl = if ($n -eq $Wtyczka) { 2 } else { 0 }
    Set-ItemProperty $k -Name LOADCTRLS -Value $ctrl -Type DWord
}

Write-Host "Ustawiono: przy nastepnym starcie BricsCAD wczyta $Wtyczka."
Pokaz
