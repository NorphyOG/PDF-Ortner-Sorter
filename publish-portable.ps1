param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = 'Stop'
$project = "PDFOrtnerSorter\PDFOrtnerSorter.csproj"
$publishDir = Join-Path -Path "PDFOrtnerSorter\bin" -ChildPath "Portable"
$zipPath = Join-Path -Path "PDFOrtnerSorter\bin" -ChildPath "PDFOrtnerSorter_Portable.zip"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

# Gracefully remove zip file if it exists
if (Test-Path $zipPath) {
    try {
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
    catch {
        Write-Warning "Could not remove old zip file immediately, will proceed anyway"
    }
}

Write-Host "Publishing portable build..."
dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -p:SelfContained=true `
    -p:PublishTrimmed=false `
    -o $publishDir | Out-Host

$pdfiumSource = Join-Path $env:USERPROFILE ".nuget\packages\pdfiumviewer.native.x86_64.v8-xfa\2018.4.8.256\Build\x64\pdfium.dll"
$pdfiumDest = Join-Path $publishDir "pdfium.dll"
if (Test-Path $pdfiumSource) {
    Copy-Item -Path $pdfiumSource -Destination $pdfiumDest -Force
    Write-Host "Copied pdfium.dll to portable folder"
}

Write-Host "Creating zip package..."
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
Write-Host "Portable build available at" $publishDir
Write-Host "Zip package created at" $zipPath
