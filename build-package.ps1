$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$buildHome = Join-Path $root '.build-home'
$env:DOTNET_CLI_HOME = $buildHome
$env:APPDATA = Join-Path $buildHome 'AppData'
$env:NUGET_PACKAGES = Join-Path $root '.nuget'
$nugetConfig = Join-Path $root 'build\NuGet.Config'
$project = Join-Path $root 'desktop\MetadataCleaner.csproj'
$dist = Join-Path $root 'dist'
$appFolder = Join-Path $dist 'app'
$setupFolder = Join-Path $dist 'setup'
$runtimeRoot = 'C:\Program Files\dotnet'
$ffmpegZip = Join-Path $root '.build-tools\ffmpeg.zip'
$ffmpegRoot = Join-Path $root '.build-tools\ffmpeg'

New-Item -ItemType Directory -Force -Path (Join-Path $env:APPDATA 'NuGet'), $appFolder, $setupFolder | Out-Null
Copy-Item -LiteralPath $nugetConfig -Destination (Join-Path $env:APPDATA 'NuGet\NuGet.Config') -Force

Write-Host 'Preparing the Windows desktop app…'
dotnet restore $project --configfile $nugetConfig --nologo
if ($LASTEXITCODE -ne 0) { throw 'The desktop app could not be restored.' }
$buildProperties = @('-p:UseAppHost=false')
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) { $buildProperties += "-p:UpdateRepository=$env:GITHUB_REPOSITORY" }
if (-not [string]::IsNullOrWhiteSpace($env:APP_VERSION)) { $buildProperties += "-p:Version=$env:APP_VERSION" }
dotnet build $project -c Release --no-restore @buildProperties --nologo
if ($LASTEXITCODE -ne 0) { throw 'The desktop app could not be built.' }
$buildOutput = Join-Path $root 'desktop\bin\Release\net8.0-windows'
foreach ($name in @('Image Metadata Cleaner.dll', 'Image Metadata Cleaner.deps.json', 'Image Metadata Cleaner.runtimeconfig.json')) {
    Copy-Item -LiteralPath (Join-Path $buildOutput $name) -Destination $appFolder -Force
}

$netVersion = Get-ChildItem (Join-Path $runtimeRoot 'shared\Microsoft.NETCore.App') -Directory | Where-Object { ([version]$_.Name).Major -eq 8 } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$desktopVersion = Get-ChildItem (Join-Path $runtimeRoot 'shared\Microsoft.WindowsDesktop.App') -Directory | Where-Object { ([version]$_.Name).Major -eq 8 } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$fxrVersion = Get-ChildItem (Join-Path $runtimeRoot 'host\fxr') -Directory | Where-Object { ([version]$_.Name).Major -eq 8 } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $netVersion -or -not $desktopVersion -or -not $fxrVersion) { throw 'A .NET 8 desktop runtime is required to create the standalone installer.' }

Write-Host 'Bundling the Windows runtime so users do not need to install it separately…'
Copy-Item -LiteralPath (Join-Path $runtimeRoot 'dotnet.exe') -Destination $appFolder -Force
$localFxr = Join-Path $appFolder (Join-Path 'host\fxr' $fxrVersion.Name)
New-Item -ItemType Directory -Force -Path $localFxr | Out-Null
Copy-Item -Path (Join-Path (Join-Path (Join-Path $runtimeRoot 'host\fxr') $fxrVersion.Name) '*') -Destination $localFxr -Recurse -Force
foreach ($entry in @(
    @{ Source = Join-Path (Join-Path $runtimeRoot 'shared\Microsoft.NETCore.App') $netVersion.Name; Destination = Join-Path (Join-Path $appFolder 'shared\Microsoft.NETCore.App') $netVersion.Name },
    @{ Source = Join-Path (Join-Path $runtimeRoot 'shared\Microsoft.WindowsDesktop.App') $desktopVersion.Name; Destination = Join-Path (Join-Path $appFolder 'shared\Microsoft.WindowsDesktop.App') $desktopVersion.Name }
)) {
    New-Item -ItemType Directory -Force -Path $entry.Destination | Out-Null
    Copy-Item -Path (Join-Path $entry.Source '*') -Destination $entry.Destination -Recurse -Force
}

if (-not (Test-Path (Join-Path $ffmpegRoot 'ffmpeg.exe'))) {
    New-Item -ItemType Directory -Force -Path (Split-Path $ffmpegZip), $ffmpegRoot | Out-Null
    Write-Host 'Downloading the FFmpeg video metadata engine…'
    Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $ffmpegZip
    Expand-Archive -LiteralPath $ffmpegZip -DestinationPath $ffmpegRoot -Force
}
$ffmpegBinary = Get-ChildItem -LiteralPath $ffmpegRoot -Filter ffmpeg.exe -Recurse | Select-Object -First 1
if (-not $ffmpegBinary) { throw 'The FFmpeg video metadata engine could not be found.' }
Copy-Item -LiteralPath $ffmpegBinary.FullName -Destination (Join-Path $appFolder 'ffmpeg.exe') -Force

$frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCompiler)) { throw 'The Windows .NET Framework compiler was not found.' }
$launcherSource = Join-Path $root 'launcher\Launcher.cs'
$launcherOutput = Join-Path $appFolder 'Image Metadata Cleaner.exe'
& $frameworkCompiler /nologo /target:winexe /out:$launcherOutput /reference:System.Windows.Forms.dll $launcherSource
if ($LASTEXITCODE -ne 0) { throw 'The app launcher could not be compiled.' }

$installerProject = Join-Path $root 'installer'
$bundle = Join-Path $installerProject 'appbundle.zip'
Compress-Archive -Path (Join-Path $appFolder '*') -DestinationPath $bundle -CompressionLevel Optimal -Force
$installerSource = Join-Path $installerProject 'Installer.cs'
$setupOutput = Join-Path $setupFolder 'Image Metadata Cleaner Setup.exe'
& $frameworkCompiler /nologo /target:winexe /out:$setupOutput /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$bundle,appbundle.zip" $installerSource
if ($LASTEXITCODE -ne 0) { throw 'The setup installer could not be compiled.' }

$deliverable = Join-Path $dist 'Image Metadata Cleaner Setup.exe'
Copy-Item -LiteralPath $setupOutput -Destination $deliverable -Force
Write-Host "Installer created: $deliverable"
