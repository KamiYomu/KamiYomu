# ---------------------------------------------------------------
# KamiYomu Windows Service Uninstaller
#
# IMPORTANT — READ BEFORE RUNNING:
#
# This script completely removes the KamiYomu Windows Service and
# ALL related files, including:
#
#   • Windows Service registration
#   • Installation directory:
#         C:\Program Files\KamiYomu
#   • User data directory:
#         C:\Users\<YourUser>\AppData\Local\KamiYomu
#   • Puppeteer environment variables:
#         PUPPETEER_SKIP_CHROMIUM_DOWNLOAD
#         PUPPETEER_EXECUTABLE_PATH
#         XDG_CONFIG_HOME
#         XDG_CACHE_HOME
#
# After uninstalling, the application will no longer be available
# at:
#
#         http://localhost:8080
#
# To uninstall:
#   - Right‑click uninstall.ps1 → "Run with PowerShell" (as Admin)
#
# ---------------------------------------------------------------

$ErrorActionPreference = "Stop"

$serviceName = "KamiYomuService"
$installDir = "C:\Program Files\KamiYomu"
$userDataDir = Join-Path $env:LOCALAPPDATA "KamiYomu"

Write-Host "Uninstalling KamiYomu Windows Service..." -ForegroundColor Cyan

# Stop service if present
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service '$serviceName'..." -ForegroundColor Yellow
    Stop-Service $serviceName -Force
}

# Delete service registration
Write-Host "Removing service registration..." -ForegroundColor Yellow
sc.exe delete $serviceName | Out-Null

# Delete installation directory
if (Test-Path $installDir) {
    Write-Host "Deleting installation directory at $installDir..." -ForegroundColor Yellow
    Remove-Item -Path $installDir -Recurse -Force
}

# Delete user data directory
if (Test-Path $userDataDir) {
    Write-Host "Deleting user data directory at $userDataDir..." -ForegroundColor Yellow
    Remove-Item -Path $userDataDir -Recurse -Force
}

# Remove environment variables
Write-Host "Removing Puppeteer environment variables..." -ForegroundColor Yellow

[Environment]::SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", $null, "User")
[Environment]::SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", $null, "User")
[Environment]::SetEnvironmentVariable("XDG_CONFIG_HOME", $null, "User")
[Environment]::SetEnvironmentVariable("XDG_CACHE_HOME", $null, "User")

Write-Host "`nUninstallation complete!" -ForegroundColor Green
Write-Host "KamiYomu has been fully removed from this system." -ForegroundColor Green
