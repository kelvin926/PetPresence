param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "artifacts/publish/PetPresence"
)

$ErrorActionPreference = "Stop"

# Build a self-contained Windows publish folder. WiX packaging can consume the output.
dotnet publish "src/PetPresence.Desktop/PetPresence.Desktop.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $Output

Write-Host "Published PetPresence desktop app to $Output"
Write-Host "Installer source: packaging/windows/PetPresence.wxs"
