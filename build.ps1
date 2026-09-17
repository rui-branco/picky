$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src"
$bin  = Join-Path $root "bin"
$csc  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }

# System.Web.Extensions (JavaScriptSerializer) is not in the compiler directory on
# every machine, so probe the usual homes and fall back to a bare name.
$extCandidates = @(
  "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Web.Extensions.dll",
  "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\System.Web.Extensions.dll",
  "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
)
$ext = $extCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $ext) { $ext = "System.Web.Extensions.dll" }
Write-Host "System.Web.Extensions: $ext"

$out = Join-Path $bin "picky.exe"
$files = Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName }

# Generate the icon if it is missing, so a clean checkout still builds branded.
$ico = Join-Path $root "assets\picky.ico"
if (-not (Test-Path $ico)) {
  Write-Host "Icon missing - generating..."
  & (Join-Path $root "tools\make-icon.ps1")
}

$args = @(
  "/nologo",
  "/target:winexe",
  "/platform:anycpu",
  "/optimize+",
  "/win32icon:$ico",
  "/out:$out",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:$ext"
) + $files

Write-Host "Compiling $($files.Count) files..."
& $csc $args
if ($LASTEXITCODE -ne 0) { throw "BUILD FAILED (csc exit $LASTEXITCODE)" }

if (-not (Test-Path $out)) { throw "BUILD FAILED: $out was not produced" }
$size = (Get-Item $out).Length
Write-Host "BUILD OK -> $out ($size bytes)"
