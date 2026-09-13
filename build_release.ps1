# Build, Obfuscate, and Package Installer Script for MovieManager Desktop
$ErrorActionPreference = "Stop"

# Resolve dotnet executable path
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCmd) { $dotnetCmd.Source } else { $null }
if (!$dotnet -or !(Test-Path $dotnet)) {
    if (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
        $dotnet = "C:\Program Files\dotnet\dotnet.exe"
    } else {
        Write-Error "dotnet SDK was not found on this machine."
        exit 1
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "0. Building MpvMenuHelper (Self-Contained) & updating MPVPlayer..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

& $dotnet publish MpvMenuHelper\MpvMenuHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o MPVPlayer
if ($LASTEXITCODE -ne 0) {
    Write-Error "MpvMenuHelper build failed."
    exit 1
}
if (Test-Path "MPVPlayer\MpvMenuHelper.pdb") {
    Remove-Item "MPVPlayer\MpvMenuHelper.pdb" -Force
}
Write-Host "MpvMenuHelper updated in MPVPlayer successfully." -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "1. Publishing Release build (Self-Contained Win-x64)..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Publish as self-contained so end-users never require installing any .NET runtime
& $dotnet publish MovieManagerDesktop.csproj -c Release -r win-x64 --self-contained true -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "2. Running Obfuscar security hardening..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

obfuscar.console obfuscar.xml
if ($LASTEXITCODE -ne 0) {
    Write-Error "Obfuscar obfuscation failed."
    exit 1
}

$obfuscatedDll = "publish\obfuscated\MovieManagerDesktop.dll"
$targetDll = "publish\MovieManagerDesktop.dll"

if (!(Test-Path $obfuscatedDll)) {
    Write-Error "Obfuscated DLL was not found at $obfuscatedDll"
    exit 1
}

Write-Host "Replacing raw DLL with obfuscated assembly..." -ForegroundColor Green
Copy-Item -Path $obfuscatedDll -Destination $targetDll -Force

Write-Host "Cleaning up intermediate obfuscation artifacts..." -ForegroundColor Yellow
if (Test-Path "publish\obfuscated") {
    Remove-Item -Path "publish\obfuscated" -Recurse -Force
}
if (Test-Path "publish\MovieManagerDesktop.pdb") {
    Remove-Item -Path "publish\MovieManagerDesktop.pdb" -Force
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "3. Compiling Inno Setup installer..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Users\ALI\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe"
)
$iscc = $null
foreach ($cand in $isccCandidates) {
    if (Test-Path $cand) { $iscc = $cand; break }
}
if (!$iscc) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found in candidate paths."
    exit 1
}

& $iscc installer.iss
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

$installer = "setup_output\MovieManager_Setup_v3.0.1.exe"
if (Test-Path $installer) {
    $item = Get-Item $installer
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "SUCCESS: Protected installer generated!" -ForegroundColor Green
    Write-Host "File: $installer ($sizeMb MB)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Error "Installer output not found at $installer"
    exit 1
}
