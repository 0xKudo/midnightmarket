# build-with-server.ps1
# Publishes ArmsFair.Server as a self-contained Windows exe and places it in
# Unity's StreamingAssets folder so it's included in every Unity build.
#
# Run from the repo root BEFORE making a Unity build:
#   .\build-with-server.ps1

$serverProject  = "ArmsFair.Server\ArmsFair.Server.csproj"
$outputDir      = "ArmsFair\Assets\StreamingAssets\Server~"

Write-Host "Building ArmsFair.Server for win-x64..."

dotnet publish $serverProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Server build failed."
    exit 1
}

Write-Host ""
Write-Host "Server published to: $outputDir"
Write-Host "Now make your Unity build as normal — the server exe will be included automatically."
