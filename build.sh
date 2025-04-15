#!/bin/bash
# Bash script to build the Google Maps Scraper

echo -e "\e[36mGoogle Maps Scraper Build Script\e[0m"
echo -e "\e[36m================================\e[0m"

# Check for dotnet
if ! command -v dotnet &> /dev/null; then
    echo -e "\e[31mError: .NET SDK not found. Please install the .NET SDK.\e[0m"
    exit 1
fi

dotnet_version=$(dotnet --version)
echo -e "\e[32mUsing .NET SDK version: $dotnet_version\e[0m"

# Check for Obfuscar
if ! dotnet tool list -g | grep -q "obfuscar"; then
    echo -e "\e[33mInstalling Obfuscar tool...\e[0m"
    dotnet tool install -g Obfuscar.GlobalTool
fi

# Clean previous builds
echo -e "\e[33mCleaning solution...\e[0m"
dotnet clean -c Release
if [ -d "publish" ]; then
    rm -rf publish
fi

# Restore packages
echo -e "\e[33mRestoring packages...\e[0m"
dotnet restore

# Build solution
echo -e "\e[33mBuilding solution...\e[0m"
dotnet build -c Release
if [ $? -ne 0 ]; then
    echo -e "\e[31mError: Build failed.\e[0m"
    exit 1
fi

# Publish app
echo -e "\e[33mPublishing application...\e[0m"
dotnet publish GoogleMapsScraper.UI/GoogleMapsScraper.UI.csproj -c Release -r win-x64 -o publish --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true

if [ $? -ne 0 ]; then
    echo -e "\e[31mError: Publish failed.\e[0m"
    exit 1
fi

# Create Obfuscar configuration
echo -e "\e[33mCreating obfuscation configuration...\e[0m"
cat > obfuscar.xml << EOL
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
EOL

# Run Obfuscar
echo -e "\e[33mObfuscating assemblies...\e[0m"
obfuscar obfuscar.xml

if [ $? -ne 0 ]; then
    echo -e "\e[31mError: Obfuscation failed.\e[0m"
    exit 1
fi

# Copy non-DLL files from publish to publish-obfuscated
echo -e "\e[33mCopying non-obfuscated files...\e[0m"
find publish -type f -not -name "*.dll" -not -name "*.pdb" -exec cp --parents {} publish-obfuscated \;

# Create zip archive
version="1.0.0"
zipFile="GoogleMapsScraper-v$version.zip"

echo -e "\e[33mCreating ZIP archive: $zipFile\e[0m"
if [ -f "$zipFile" ]; then
    rm "$zipFile"
fi

# Check if zip command is available
if command -v zip &> /dev/null; then
    (cd publish-obfuscated && zip -r "../$zipFile" *)
else
    echo -e "\e[33mZip command not found. Skipping archive creation.\e[0m"
    echo -e "\e[33mYou can manually zip the 'publish-obfuscated' directory.\e[0m"
fi

echo -e "\e[32mBuild completed successfully!\e[0m"
echo -e "\e[36mOutput: $zipFile\e[0m"
echo -e "\e[36mYou can also find the uncompressed build in the 'publish-obfuscated' directory.\e[0m" 