# -------------------------------
# KamiYomu ASP.NET Windows Service Installer
# -------------------------------

$ErrorActionPreference = "Stop"

$serviceName = "KamiYomuService"
$displayName = "KamiYomu Service"
$description = "KamiYomu background service"
$installDir = "C:\Program Files\KamiYomu"
$package = "kamiyomu-1.2.1-win-x64.tar.gz"

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
