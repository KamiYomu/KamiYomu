# ---------------------------------------------------------------
# KamiYomu Windows Service Installer (Auto-Download)
#
# Features:
#   • Fetch the 5 most recent releases from GitHub
#   • Latest version is marked as "(recommended)"
#   • If user presses ENTER → latest version is auto-selected
#   • User selects Windows asset ("win")
#   • Download to TEMP
#   • Install Windows Service
#   • Cleanup downloaded file
# ---------------------------------------------------------------

$ErrorActionPreference = "Stop"

$serviceName = "KamiYomuService"
$displayName = "KamiYomu Service"
$description = "KamiYomu background service"
$installDir = "C:\Program Files\KamiYomu"

Write-Host "Fetching KamiYomu releases from GitHub..." -ForegroundColor Cyan

# Fetch all releases
$releases = Invoke-RestMethod -Uri "https://api.github.com/repos/KamiYomu/KamiYomu/releases" -Headers @{ "User-Agent" = "PowerShell" }

# Sort by published date (newest first) and take top 5
$recent = $releases | Sort-Object published_at -Descending | Select-Object -First 5

if ($recent.Count -eq 0) {
    Write-Host "ERROR: No releases found." -ForegroundColor Red
    exit 1
}

Write-Host "`nMost recent 5 versions:" -ForegroundColor Cyan

# Show versions (mark the newest as recommended)
for ($i = 0; $i -lt $recent.Count; $i++) {
    $tag = $recent[$i].tag_name
    if ($i -eq 0) {
        Write-Host "[$i] $tag  (recommended)"
    } else {
        Write-Host "[$i] $tag"
    }
}

# Ask user to choose version
$versionSelection = Read-Host "`nEnter the number of the version you want to install (ENTER = recommended)"

# If user presses ENTER → auto-select latest
if ([string]::IsNullOrWhiteSpace($versionSelection)) {
    $versionSelection = 0
    Write-Host "Using recommended version..." -ForegroundColor Yellow
}

# Validate selection
if ($versionSelection -notmatch '^\d+$' -or [int]$versionSelection -ge $recent.Count) {
    Write-Host "Invalid selection." -ForegroundColor Red
    exit 1
}

$chosenRelease = $recent[$versionSelection]
$version = $chosenRelease.tag_name

Write-Host "`nSelected version: ${version}" -ForegroundColor Green

# Filter Windows assets
$winAssets = $chosenRelease.assets | Where-Object { $_.name -match "win" }

if ($winAssets.Count -eq 0) {
    Write-Host "ERROR: No Windows-compatible assets found for version ${version}." -ForegroundColor Red
    exit 1
}

Write-Host "`nAvailable Windows packages for version ${version}:" -ForegroundColor Cyan

# Show Windows assets
for ($i = 0; $i -lt $winAssets.Count; $i++) {
    Write-Host "[$i] $($winAssets[$i].name)"
}

# Ask user to choose Windows asset
$assetSelection = Read-Host "`nEnter the number of the Windows package you want to install"

if ($assetSelection -notmatch '^\d+$' -or [int]$assetSelection -ge $winAssets.Count) {
    Write-Host "Invalid selection." -ForegroundColor Red
    exit 1
}

$chosenAsset = $winAssets[$assetSelection]
$packageName = $chosenAsset.name
$downloadUrl = $chosenAsset.browser_download_url

Write-Host "`nSelected package: ${packageName}" -ForegroundColor Green

# TEMP directory for download
$tempDir = Join-Path $env:TEMP "KamiYomu"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

$tempFile = Join-Path $tempDir $packageName

Write-Host "Downloading package to ${tempFile} ..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile

Write-Host "Download complete." -ForegroundColor Green

# Stop and remove existing service if present
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Existing service found. Stopping..." -ForegroundColor Yellow
    Stop-Service $serviceName -Force
    sc.exe delete $serviceName | Out-Null
}

# Create installation directory
Write-Host "Creating installation directory at ${installDir}" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Extract package
Write-Host "Extracting package ${packageName} ..." -ForegroundColor Cyan
tar -xzf $tempFile -C $installDir

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

# Cleanup
Write-Host "Cleaning up downloaded package..." -ForegroundColor Cyan
Remove-Item -Path $tempFile -Force

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "Service '${serviceName}' is now running." -ForegroundColor Green
Write-Host "Downloaded package removed from TEMP." -ForegroundColor Green
Write-Host "Visit: http://localhost:8080"