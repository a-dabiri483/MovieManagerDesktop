# Build, Obfuscate, and Package Installer Script for MovieManager Desktop
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Publishing Release build..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Publish without PDBs for clean release
dotnet publish MovieManagerDesktop.csproj -c Release -o publish
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

$iscc = "C:\Users\ALI\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (!(Test-Path $iscc)) {
    Write-Error "Inno Setup Compiler not found at $iscc"
    exit 1
}

& $iscc installer.iss
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
    exit 1
}

$installer = "setup_output\MovieManager_Setup_v2.7.0.exe"
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
