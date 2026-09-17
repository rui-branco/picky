# Renders README screenshots from the real app running with --demo, so profile
# names and emails are placeholders.
#
# Uses PrintWindow(PW_RENDERFULLCONTENT), which asks the window to draw ITSELF
# into a bitmap. Screen scraping (Graphics.CopyFromScreen) must never be used
# here: it copies whatever pixels are on screen, so anything overlapping the
# window - a browser, an inbox, a sign-in prompt - ends up in a published image.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe  = Join-Path $root "bin\picky.exe"
$docs = Join-Path $root "docs"
if (-not (Test-Path $exe)) { throw "build first: $exe missing" }
if (-not (Test-Path $docs)) { New-Item -ItemType Directory -Path $docs | Out-Null }

Add-Type @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
public class Cap {
  public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
  delegate bool EnumProc(IntPtr h, IntPtr p);

  public static List<IntPtr> Visible(uint target) {
    var res = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr p) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 200 && r.B - r.T > 100) res.Add(h);
      }
      return true;
    }, IntPtr.Zero);
    return res;
  }

  public static RECT Frame(IntPtr h) {
    RECT r;
    if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) == 0) return r;
    GetWindowRect(h, out r); return r;
  }

  // Renders the window's own pixels; occluding windows cannot bleed in.
  public static Bitmap Shot(IntPtr h) {
    RECT f = Frame(h);
    RECT w; GetWindowRect(h, out w);
    int fullW = w.R - w.L, fullH = w.B - w.T;
    if (fullW <= 0 || fullH <= 0) return null;

    Bitmap full = new Bitmap(fullW, fullH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    using (Graphics g = Graphics.FromImage(full)) {
      IntPtr hdc = g.GetHdc();
      bool ok = PrintWindow(h, hdc, 2);   // PW_RENDERFULLCONTENT
      g.ReleaseHdc(hdc);
      if (!ok) { full.Dispose(); return null; }
    }

    // Trim the invisible resize border so the image is exactly the drawn frame.
    int dx = f.L - w.L, dy = f.T - w.T;
    int cw = f.R - f.L, ch = f.B - f.T;
    if (dx < 0 || dy < 0 || cw <= 0 || ch <= 0 || dx + cw > fullW || dy + ch > fullH) return full;
    Bitmap crop = full.Clone(new Rectangle(dx, dy, cw, ch), full.PixelFormat);
    full.Dispose();
    return crop;
  }
}
"@ -ReferencedAssemblies System.Drawing, System.Windows.Forms

function Shoot([string[]]$argList, [string]$file, [bool]$round) {
    $p = Start-Process $exe -ArgumentList $argList -PassThru
    Start-Sleep -Milliseconds 2000

    $handles = [Cap]::Visible([uint32]$p.Id)
    if ($handles.Count -eq 0) { try { $p.Kill() } catch { }; throw "no window for: $argList" }

    $bmp = [Cap]::Shot($handles[0])
    try { $p.Kill() } catch { }
    if ($bmp -eq $null) { throw "PrintWindow failed for: $argList" }

    if ($round) {
        $w = $bmp.Width; $h = $bmp.Height
        $out = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($out)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)
        $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = 24
        $gp.AddArc(0, 0, $d, $d, 180, 90)
        $gp.AddArc($w - $d - 1, 0, $d, $d, 270, 90)
        $gp.AddArc($w - $d - 1, $h - $d - 1, $d, $d, 0, 90)
        $gp.AddArc(0, $h - $d - 1, $d, $d, 90, 90)
        $gp.CloseFigure()
        $g.SetClip($gp)
        $g.DrawImage($bmp, 0, 0)
        $g.Dispose(); $bmp.Dispose()
        $bmp = $out
    }

    $path = Join-Path $docs $file
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("  {0}  ({1}x{2})" -f $file, $bmp.Width, $bmp.Height)
    $bmp.Dispose()
    Start-Sleep -Milliseconds 300
}

Write-Host "Rendering (PrintWindow, no screen scraping):"
Shoot @("--demo") "settings.png" $false
Shoot @("--demo", "--keep", "https://developer.mozilla.org/en-US/docs/Web") "picker.png" $true
Write-Host "Done -> $docs"
