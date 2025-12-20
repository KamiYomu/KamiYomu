<#
.SYNOPSIS
Installs Kamiyomu Windows service from GitHub release artifact
#>

param(
    [string]$Repo = "Kamiyomu/Kamiyomu", 
    [string]$VersionTag = "v1.0.0",            # GitHub release tag
    [string]$Arch = "win-x64",                 # artifact architecture
    [string]$InstallDir = "C:\Program Files\Kamiyomu",
    [string]$ServiceName = "KamiyomuService",
    [string]$Executable = "Kamiyomu.exe"
)

# Ensure running as Administrator
If (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    Write-Error "Run as Administrator!"
    Exit 1
}

# GitHub API: Get release asset URL
$ReleaseApi = "https://api.github.com/repos/$Repo/releases/tags/$VersionTag"
Write-Host "Fetching release info from $ReleaseApi ..."
$ReleaseInfo = Invoke-RestMethod -Uri $ReleaseApi -UseBasicParsing

# Find the asset that matches the architecture
$Asset = $ReleaseInfo.assets | Where-Object { $_.name -like "*$Arch*.zip" }
If (-Not $Asset) {
    Write-Error "Artifact for architecture '$Arch' not found in release $VersionTag!"
    Exit 1
}

$DownloadUrl = $Asset.browser_download_url
$ZipFile = "$env:TEMP\$($Asset.name)"
Write-Host "Downloading $($Asset.name) ..."
Invoke-WebRequest -Uri $DownloadUrl -OutFile $ZipFile

# Create install directory
If (-Not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Extract artifact
Write-Host "Extracting artifact to $InstallDir ..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($ZipFile, $InstallDir, $true)

# Path to the executable
$ExePath = Join-Path $InstallDir $Executable

# Remove old service if exists
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping and removing old service..."
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create Windows service
Write-Host "Creating Windows service '$ServiceName' ..."
New-Service -Name $ServiceName `
            -BinaryPathName "`"$ExePath`"" `
            -DisplayName "Kamiyomu Service" `
            -Description "Runs Kamiyomu with Hangfire" `
            -StartupType Automatic

# Start the service
Start-Service -Name $ServiceName

Write-Host "Kamiyomu installed and running!"
