# Packs the Windows head as one MSIX per architecture. Runs inside the Parallels
# Windows VM, because MSIX can only be assembled on Windows.
#
#   tools/build_windows.ps1 -Source <repo> -Out <Releases/version> -Version 1.0.0 -Build 1
#
# The source is copied off the shared Mac folder first: building straight from a
# Parallels share makes dotnet's file locking unreliable.
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Version = "1.0.0",
    [string]$Build = "1"
)

$ErrorActionPreference = "Stop"
$work = "C:\pssbuild"

# prlctl exec does not reliably hand back a long run's output, so record everything
# next to the packages instead and let the Mac read it from the shared folder.
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$transcript = Join-Path $Out "windows-build.log"
Start-Transcript -Path $transcript -Force | Out-Null
trap {
    $_ | Out-String | Write-Host
    "failed" | Set-Content (Join-Path $Out "windows-status.txt")
    Stop-Transcript | Out-Null
    exit 1
}
$src = Join-Path $work "src"
$icon = Join-Path $work "tools"

function Step($text) { Write-Host ""; Write-Host "== $text" }

# ------------------------------------------------------------------ 1. source
Step "copying the source"
New-Item -ItemType Directory -Force -Path $work, $icon | Out-Null
robocopy $Source $src /MIR /XD bin obj .git .vs .idea Releases Stores TestResults `
    /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with $LASTEXITCODE" }

# ------------------------------------------------------------------- 2. packer
Step "locating makeappx and signtool"
$makeappx = $null
$signtool = $null
$kits = "C:\Program Files (x86)\Windows Kits\10\bin"
if (Test-Path $kits) {
    $hit = Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64" } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($hit) { $makeappx = $hit.FullName }
    $hit = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "x64" } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($hit) { $signtool = $hit.FullName }
}
if (-not $makeappx) {
    # No Windows SDK installed: the SDK build tools ship makeappx inside a NuGet
    # package, and dotnet restore fetches and verifies it for us.
    $index = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/index.json"
    $latest = $index.versions | Where-Object { $_ -notmatch "-" } | Select-Object -Last 1
    $ref = Join-Path $icon "sdkref"
    New-Item -ItemType Directory -Force -Path $ref | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$latest" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $ref "sdkref.csproj") -Encoding UTF8
    dotnet restore (Join-Path $ref "sdkref.csproj") -v q
    if ($LASTEXITCODE -ne 0) { throw "could not restore Microsoft.Windows.SDK.BuildTools" }

    $root = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools\$latest"
    $makeappx = (Get-ChildItem $root -Recurse -Filter makeappx.exe |
        Where-Object { $_.FullName -match "x64" } | Select-Object -First 1).FullName
    $signtool = (Get-ChildItem $root -Recurse -Filter signtool.exe |
        Where-Object { $_.FullName -match "x64" } | Select-Object -First 1).FullName
    Write-Host "   using Microsoft.Windows.SDK.BuildTools $latest"
}
if (-not $makeappx) { throw "makeappx.exe not found" }
Write-Host "   makeappx: $makeappx"

# ----------------------------------------------------------------- 3. publish
function Publish($rid) {
    Step "publishing $rid"
    $out = Join-Path $work "pub\$rid"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    # Swallow the build output: whatever a function emits becomes its return value.
    $log = dotnet publish (Join-Path $src "PSSCollection.WindowsDX") -c Release -r $rid --self-contained true `
        -p:EnableWindowsTargeting=true -p:Version=$Version -o $out -v q 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Write-Host $log; throw "publish failed for $rid" }
    return $out
}

# ---------------------------------------------------------------- 4. manifest
function Manifest($arch) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load((Join-Path $src "PSSCollection.WindowsDX\Package.appxmanifest"))
    $identity = $xml.Package.Identity
    $identity.SetAttribute("ProcessorArchitecture", $arch)
    $identity.SetAttribute("Version", "$Version.0")
    $path = Join-Path $work "AppxManifest-$arch.xml"
    $xml.Save($path)
    return $path
}

# ------------------------------------------------------------------ 5. package
$outputs = @()
foreach ($arch in @("x64", "arm64")) {
    $pub = Publish "win-$arch"
    $stage = Join-Path $work "stage\$arch"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    # The publish already carries Assets\ (it is Content in the csproj), so copying it again
    # would nest a second Assets folder inside the package.
    Copy-Item "$pub\*" $stage -Recurse -Force
    Copy-Item (Manifest $arch) (Join-Path $stage "AppxManifest.xml") -Force

    # The tile manifest points at these; makeappx rejects a package that lacks them.
    foreach ($asset in @("StoreLogo.png", "Square44x44Logo.png", "Square150x150Logo.png",
                         "Square310x310Logo.png", "Wide310x150Logo.png")) {
        $p = Join-Path $stage "Assets\$asset"
        if (-not (Test-Path $p)) { throw "missing tile asset $asset" }
    }

    $msix = Join-Path $work "out\PSS-Collection-$Version-win-$arch.msix"
    New-Item -ItemType Directory -Force -Path (Split-Path $msix) | Out-Null
    Step "packing $arch MSIX"
    & $makeappx pack /d $stage /p $msix /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx failed for $arch" }
    $outputs += $msix
}

# ------------------------------------------------------------------- 6. sign
# Partner Center re-signs Store submissions, but a signed package can be
# sideloaded for testing. The certificate subject is the Store publisher identity.
$pfx = Join-Path $icon "pss-store.pfx"
if (Test-Path $pfx) {
    Step "signing"
    foreach ($msix in $outputs) {
        & $signtool sign /fd SHA256 /f $pfx (Get-Content (Join-Path $icon "pss-store.pass") -Raw).Trim() $msix
        if ($LASTEXITCODE -ne 0) { Write-Host "   warning: signtool failed for $msix" }
    }
} else {
    Write-Host "note: no test certificate at $pfx; the MSIX files are unsigned (Partner Center signs them)"
}

# ------------------------------------------------------------- 7. copy back
Step "copying the packages to the Mac"
New-Item -ItemType Directory -Force -Path $Out | Out-Null
foreach ($msix in $outputs) {
    Copy-Item $msix (Join-Path $Out (Split-Path $msix -Leaf)) -Force
    $item = Get-Item (Join-Path $Out (Split-Path $msix -Leaf))
    Write-Host ("   {0}  {1:N1} MB" -f $item.Name, ($item.Length / 1MB))
}

"ok" | Set-Content (Join-Path $Out "windows-status.txt")
Stop-Transcript | Out-Null
