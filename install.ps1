# Builds Picky, installs it to %LOCALAPPDATA%\Picky, registers it as a browser
# and adds a Start Menu shortcut.
#
# Install here rather than registering bin\picky.exe directly: the registration
# stores an absolute path, so pointing Windows at a build output means deleting
# or moving the project breaks link handling system-wide.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA "Picky"
$exe  = Join-Path $dest "picky.exe"

Write-Host "Building..."
& (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) { throw "build failed" }

# A running instance holds a lock on the exe and would fail the copy.
Get-Process picky -ErrorAction Ignore | ForEach-Object {
    Write-Host "  stopping running instance (PID $($_.Id))"
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 400

if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }
Copy-Item (Join-Path $root "bin\picky.exe") $exe -Force
Write-Host "Installed -> $exe"

Start-Process $exe -ArgumentList "--register" -Wait
Start-Sleep -Milliseconds 600

$lnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Picky.lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $dest
$sc.IconLocation = "$exe,0"
$sc.Description = "Choose which browser opens each link"
$sc.Save()
Write-Host "Shortcut  -> $lnk"

# Registering adds Picky to startup; start it now too, so the very next link is
# already a hand-off rather than a cold start.
Start-Process $exe -ArgumentList "--background"

$handler = (Get-ItemProperty "HKCU:\SOFTWARE\Classes\PickyURL\shell\open\command" -ErrorAction SilentlyContinue).'(default)'
$choice = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice" -ErrorAction SilentlyContinue).ProgId

Write-Host ""
Write-Host "Handler   : $handler"
if ($choice -eq "PickyURL") {
    Write-Host "Status    : Picky is your default http handler."
} else {
    Write-Host "Status    : registered, but http is still handled by '$choice'."
    Write-Host "            Open Picky and click 'Set as default' to finish."
}
