# ---------------------------------------------------------------
# KamiYomu Windows Service Installer
#
# IMPORTANT — READ BEFORE RUNNING:
#
# 1. This script must be executed from the SAME folder where you
#    downloaded the application package:
#
#        kamiyomu-x.x.x-win-x64.tar.gz
#
#    The installer expects the .tar.gz file to be present in the
#    current working directory so it can extract and install the
#    application into:
#
#        C:\Program Files\KamiYomu
#
# 2. After installation, the Windows Service will automatically
#    start and the KamiYomu application will be available at:
#
#        http://localhost:8080
#
#    This is the fixed port configured for the service.
#
# 3. To install:
#       - Place install.ps1 and kamiyomu-x.x.x-win-x64.tar.gz together
#       - Right‑click install.ps1 → "Run with PowerShell" (as Admin)
#
# The installer will:
#    • Extract the application files
#    • Register the Windows Service
#    • Configure automatic restart on failure
#    • Start the service immediately
# ---------------------------------------------------------------


$ErrorActionPreference = "Stop"

$serviceName = "KamiYomuService"
$displayName = "KamiYomu Service"
$description = "KamiYomu background service"
$installDir = "C:\Program Files\KamiYomu"
$package = "kamiyomu-x.x.x-win-x64.tar.gz" # Update this to match the actual package name you downloaded

Write-Host "Installing $displayName ..." -ForegroundColor Cyan

# Stop and remove existing service if present
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Existing service found. Stopping..." -ForegroundColor Yellow
    Stop-Service $serviceName -Force
    sc.exe delete $serviceName | Out-Null
}

# Create installation directory
Write-Host "Creating installation directory at $installDir" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Extract package
Write-Host "Extracting package $package ..." -ForegroundColor Cyan
tar -xzf $package -C $installDir

# Find the executable
$exe = Get-ChildItem -Path $installDir -Filter "*.exe" -Recurse | Select-Object -First 1

if (-not $exe) {
    Write-Host "ERROR: No executable found in extracted package." -ForegroundColor Red
    exit 1
}

Write-Host "Executable detected: $($exe.FullName)" -ForegroundColor Green

# Install Windows Service
Write-Host "Registering Windows Service..." -ForegroundColor Cyan

New-Service `
    -Name $serviceName `
    -BinaryPathName "`"$($exe.FullName)`"" `
    -DisplayName $displayName `
    -Description $description `
    -StartupType Automatic

# Configure recovery options (restart on crash)
Write-Host "Configuring service recovery options..." -ForegroundColor Cyan
sc.exe failure $serviceName reset= 86400 actions= restart/60000 | Out-Null

# Start service
Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service $serviceName

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "Service '$serviceName' is now running." -ForegroundColor Green
