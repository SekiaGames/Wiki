param(
  [string]$Action = "status",
  [string]$ScriptPath = "",
  [string]$Arguments = ""
)

# Manage the shell:startup shortcut for the Asset Wiki launcher.
# Read/write scope: one .lnk file inside the current user's Startup folder.

$ErrorActionPreference = "Stop"

$startup = [Environment]::GetFolderPath("Startup")
$linkName = "WorldEditor Asset Wiki.lnk"
$linkPath = Join-Path $startup $linkName

function Show-Status {
  if (Test-Path -LiteralPath $linkPath) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($linkPath)
    Write-Host "[ON ] Autostart enabled"
    Write-Host "      link   : $linkPath"
    Write-Host "      target : $($shortcut.TargetPath)"
    Write-Host "      args   : $($shortcut.Arguments)"
  }
  else {
    Write-Host "[OFF] Autostart is not installed"
    Write-Host "      expected: $linkPath"
  }
}

switch ($Action.ToLower()) {
  "add" {
    if (-not $ScriptPath) {
      Write-Host "[ERROR] Missing launcher path"
      exit 1
    }
    if (-not (Test-Path -LiteralPath $ScriptPath)) {
      Write-Host "[ERROR] Launcher not found: $ScriptPath"
      exit 1
    }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $ScriptPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = Split-Path -Parent $ScriptPath
    $shortcut.WindowStyle = 7
    $shortcut.Description = "Start the read-only War3 asset wiki server"
    $shortcut.IconLocation = "$env:SystemRoot\System32\cmd.exe,0"
    $shortcut.Save()
    Write-Host "[OK ] Autostart enabled"
    Show-Status
  }
  "remove" {
    if (Test-Path -LiteralPath $linkPath) {
      Remove-Item -LiteralPath $linkPath -Force
      Write-Host "[OK ] Autostart removed: $linkPath"
    }
    else {
      Write-Host "[INFO] Autostart was not installed"
    }
  }
  default {
    Show-Status
  }
}
