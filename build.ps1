#!/usr/bin/env pwsh
# PowerShell script to build and obfuscate the Google Maps Scraper

Write-Host "Google Maps Scraper Build Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Check for dotnet
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "Error: .NET SDK not found. Please install the .NET SDK." -ForegroundColor Red
    exit 1
}

# Check for Obfuscar
$obfuscarInstalled = dotnet tool list -g | Select-String -Pattern "obfuscar"
if (-not $obfuscarInstalled) {
    Write-Host "Installing Obfuscar tool..." -ForegroundColor Yellow
    dotnet tool install -g Obfuscar.GlobalTool
}

# Clean previous builds
Write-Host "Cleaning solution..." -ForegroundColor Yellow
dotnet clean -c Release
if (Test-Path "publish") {
    Remove-Item -Path "publish" -Recurse -Force
}

# Restore packages
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed." -ForegroundColor Red
    exit 1
}

# Publish app
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish GoogleMapsScraper.UI/GoogleMapsScraper.UI.csproj -c Release -r win-x64 -o publish --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Publish failed." -ForegroundColor Red
    exit 1
}

# Create Obfuscar configuration
$obfuscarConfig = @"
<?xml version='1.0'?>
<Obfuscator>
  <Var name="InPath" value="publish" />
  <Var name="OutPath" value="publish-obfuscated" />
  <Var name="KeepPublicApi" value="false" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="RenameProperties" value="true" />
  <Var name="RenameEvents" value="true" />
  <Var name="RenameFields" value="true" />
  <Var name="RegenerateDebugInfo" value="false" />

  <Module file="GoogleMapsScraper.UI.dll">
    <SkipType name="Program" />
  </Module>
  
  <Module file="GoogleMapsScraper.Core.dll" />
</Obfuscator>
"@

$obfuscarConfig | Out-File -FilePath "obfuscar.xml" -Encoding UTF8

# Run Obfuscar
Write-Host "Obfuscating assemblies..." -ForegroundColor Yellow
obfuscar obfuscar.xml

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Obfuscation failed." -ForegroundColor Red
    exit 1
}

# Copy non-DLL files from publish to publish-obfuscated
Write-Host "Copying non-obfuscated files..." -ForegroundColor Yellow
Get-ChildItem -Path "publish" -Exclude "*.dll", "*.pdb" | Copy-Item -Destination "publish-obfuscated" -Recurse -Force

# Create zip archive
$version = "1.0.0"
$zipFile = "GoogleMapsScraper-v$version.zip"

Write-Host "Creating ZIP archive: $zipFile" -ForegroundColor Yellow
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory("publish-obfuscated", $zipFile)

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output: $zipFile" -ForegroundColor Cyan
Write-Host "You can also find the uncompressed build in the 'publish-obfuscated' directory." -ForegroundColor Cyan 