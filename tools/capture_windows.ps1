# Captures Windows store screenshots by driving the game with keystrokes in the
# logged-in session and copying its client area to a PNG.
#
#   tools/capture_windows.ps1 -App C:\pssbuild\pub\win-x64\PSSCollection.exe -Out \\Mac\Home\...\shots
param(
    [Parameter(Mandatory = $true)][string]$App,
    [Parameter(Mandatory = $true)][string]$Out
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hgt, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2;
        public int dmPanningWidth, dmPanningHeight;
    }
    [DllImport("user32.dll")] public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);
}
"@

# --- ask for a 1920x1080 desktop, which is what the store wants -----------------
$mode = New-Object Win+DEVMODE
$mode.dmSize = [Runtime.InteropServices.Marshal]::SizeOf($mode)
$mode.dmPelsWidth = 1920
$mode.dmPelsHeight = 1080
$mode.dmBitsPerPel = 32
$mode.dmFields = 0x40000 -bor 0x80000 -bor 0x10000   # PELSWIDTH | PELSHEIGHT | BITSPERPEL
$null = [Win]::ChangeDisplaySettings([ref]$mode, 0)
Start-Sleep -Seconds 2
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
"" | Out-File (Join-Path $Out "display.txt")
"display $($bounds.Width)x$($bounds.Height)" | Out-File (Join-Path $Out "display.txt") -Append

function Start-Game {
    Get-Process PSSCollection -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Kill(); $_.WaitForExit(5000)
    }
    Start-Sleep -Milliseconds 800
    $p = Start-Process -FilePath $App -PassThru
    $h = [IntPtr]::Zero
    for ($i = 0; $i -lt 40 -and $h -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 500
        $p.Refresh()
        $h = $p.MainWindowHandle
    }
    if ($h -eq [IntPtr]::Zero) { throw "no window from $App" }
    [Win]::ShowWindow($h, 9) | Out-Null
    [Win]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, 0, 0, 0x0001 -bor 0x0004) | Out-Null

    # Size the client area to exactly 1280x720 so the capture is the game's native
    # resolution rather than whatever the display scaling produced.
    $client = New-Object Win+RECT
    [Win]::GetClientRect($h, [ref]$client) | Out-Null
    $frameW = 0
    $frameH = 0
    if ($client.Right -gt 0) {
        $wr = New-Object Win+RECT
        [Win]::GetWindowRect($h, [ref]$wr) | Out-Null
        $frameW = ($wr.Right - $wr.Left) - $client.Right
        $frameH = ($wr.Bottom - $wr.Top) - $client.Bottom
    }
    [Win]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, 1280 + $frameW, 720 + $frameH, 0x0004) | Out-Null
    $ws = New-Object -ComObject WScript.Shell
    $null = $ws.AppActivate($p.Id)
    [Win]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 900
    return $p
}

function Shot($p, $name) {
    [Win]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 250
    $rect = New-Object Win+RECT
    [Win]::GetClientRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $origin = New-Object Win+POINT
    [Win]::ClientToScreen($p.MainWindowHandle, [ref]$origin) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bmp.Size)
    $bmp.Save((Join-Path $Out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    "$name ${w}x${h}" | Out-File (Join-Path $Out "shots.txt") -Append
}

function Key($p, $keys, $wait = 0.9) {
    $ws = New-Object -ComObject WScript.Shell
    $null = $ws.AppActivate($p.Id)
    [Win]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds ([int]($wait * 1000))
}

# A real mouse click is far more dependable than synthetic keystrokes: it also gives
# the window focus, and the client area is exactly the game's 1280x720 canvas.
function Click($p, $x, $y, $wait = 0.9) {
    $pt = New-Object Win+POINT
    $pt.X = $x
    $pt.Y = $y
    [Win]::ClientToScreen($p.MainWindowHandle, [ref]$pt) | Out-Null
    [Win]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 120
    [Win]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 90
    [Win]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds ([int]($wait * 1000))
}

"" | Out-File (Join-Path $Out "shots.txt")
$games = @("invaders", "centipede", "hopper", "storm", "lightcycle")

$p = Start-Game
Shot $p "title"
Click $p 640 390 1.4          # leave the title
Shot $p "menu"

$cards = @(@(234, 219), @(640, 219), @(1046, 219), @(234, 445), @(640, 445))

foreach ($index in 0..4) {
    $p = Start-Game
    Click $p 640 390 1.2                        # title -> menu
    Click $p $cards[$index][0] $cards[$index][1] 1.2
    Click $p 640 302 3.6                        # SKILLED
    if ($games[$index] -eq "storm") { Click $p 500 300 0.4; Click $p 800 220 0.5 }
    if ($games[$index] -eq "lightcycle") { Key $p "{UP}" 0.5; Key $p "{LEFT}" 0.5 }
    Shot $p $games[$index]
}

Get-Process PSSCollection -ErrorAction SilentlyContinue | Stop-Process -Force
"done" | Out-File (Join-Path $Out "status.txt")
