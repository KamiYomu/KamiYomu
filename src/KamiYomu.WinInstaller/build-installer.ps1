# ================================
# 1. Ensure nuget.exe exists
# ================================
$nugetPath = "./nuget.exe"

if (-Not (Test-Path $nugetPath)) {
    Write-Host "nuget.exe not found. Downloading..."
    Invoke-WebRequest `
        -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" `
        -OutFile $nugetPath
    Write-Host "nuget.exe downloaded successfully."
}
else {
    Write-Host "nuget.exe already exists. Skipping download."
}

# ================================
# 2. Ensure WiX is installed locally
# ================================
$wixPackageRoot = "./packages"
$wixToolsPath = Get-ChildItem -Path $wixPackageRoot -Recurse -Filter "candle.exe" -ErrorAction SilentlyContinue |
                Select-Object -First 1 |
                ForEach-Object { Split-Path $_.FullName }

if (-Not $wixToolsPath) {
    Write-Host "WiX not found. Installing via nuget..."
    ./nuget.exe install Wix -Version 3.14.0 -OutputDirectory "./packages"
    Write-Host "WiX installed successfully."

    # Re-scan for tools
    $wixToolsPath = Get-ChildItem -Path $wixPackageRoot -Recurse -Filter "candle.exe" |
                    Select-Object -First 1 |
                    ForEach-Object { Split-Path $_.FullName }
}

Write-Host "Using WiX tools at: $wixToolsPath"

# ================================
# 3. Harvest architecture folders
# ================================
$installer = "."
$output = "$installer/bin"

Write-Host "Harvesting architecture folders with Heat..."

# Changed -ag to -gg to ensure hardcoded GUIDs are generated for Directory components
# This prevents the LGHT0230 error.

# ARM64
& "$wixToolsPath/heat.exe" `
    dir "assets/win-arm64" `
    -cg WorkerAppArm64Files `
    -dr Arm64Root `
    -var var.Arm64Root `
    -gg -pprefix ARM64_ `
    -sreg -srd -ke `
    -x "KamiYomu.Web.exe" `
    -out "$installer/WorkerAppArm64Files.wxs"

# X64
& "$wixToolsPath/heat.exe" `
    dir "assets/win-x64" `
    -cg WorkerAppX64Files `
    -dr X64Root `
    -var var.X64Root `
    -gg -pprefix X64_ `
    -sreg -srd -ke `
    -x "KamiYomu.Web.exe" `
    -out "$installer/WorkerAppX64Files.wxs"

# X86
& "$wixToolsPath/heat.exe" `
    dir "assets/win-x86" `
    -cg WorkerAppX86Files `
    -dr X86Root `
    -var var.X86Root `
    -gg -pprefix X86_ `
    -sreg -srd -ke `
    -x "KamiYomu.Web.exe" `
    -out "$installer/WorkerAppX86Files.wxs"


Write-Host "Heat harvesting complete."

# ================================
# 4. Build MSI
# ================================
if (Test-Path $output) { Remove-Item -Recurse -Force $output }
New-Item -ItemType Directory -Force -Path $output | Out-Null

Write-Host "Compiling WiX sources..."

& "$wixToolsPath/candle.exe" `
    "$installer/KamiYomu.wxs" `
    "$installer/ServiceConfig.wxs" `
    "$installer/WorkerAppArm64Files.wxs" `
    "$installer/WorkerAppX64Files.wxs" `
    "$installer/WorkerAppX86Files.wxs" `
    -dArm64Root="assets/win-arm64" `
    -dX64Root="assets/win-x64" `
    -dX86Root="assets/win-x86" `
    -ext WixUtilExtension `
    -out "$output/"

# Check if compilation succeeded before linking
if ($LASTEXITCODE -eq 0) {
    Write-Host "Linking MSI..."
    & "$wixToolsPath/light.exe" `
        "$output/KamiYomu.wixobj" `
        "$output/ServiceConfig.wixobj" `
        "$output/WorkerAppArm64Files.wixobj" `
        "$output/WorkerAppX64Files.wixobj" `
        "$output/WorkerAppX86Files.wixobj" `
        -ext WixUtilExtension `
        -sw1076 -sw1056 `
        -o "$output/KamiYomu.msi"
    Write-Host "MSI generated at: $output/KamiYomu.msi"
} else {
    Write-Error "Compilation failed. Check the errors above."
}