[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$OutputDirectory,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = [Environment]::GetEnvironmentVariable('OSIRIS_RELEASE_SOURCE_ROOT')
}

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    throw 'Pass -SourceRoot with the path to a prepared Osiris installation.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\artifacts'
}

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )

    if (-not $Child.StartsWith($Parent + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe path '$Child' is not inside '$Parent'."
    }
}

$repositoryRoot = Get-FullPath (Join-Path $PSScriptRoot '..')
$sourceRootPath = Get-FullPath $SourceRoot
$outputRoot = Get-FullPath $OutputDirectory
$sourceApp = Join-Path $sourceRootPath 'App'
$versionFile = Join-Path $repositoryRoot 'version.json'
$programUpdateTool = Join-Path $repositoryRoot 'tools\InheritedProgramUpdateGuard\InheritedProgramUpdateGuard.csproj'
$protocolHookTool = Join-Path $repositoryRoot 'tools\InheritedProtocolGuard\InheritedProtocolGuard.csproj'
$startupSplashTool = Join-Path $repositoryRoot 'tools\InheritedStartupSplashGuard\InheritedStartupSplashGuard.csproj'
$osirisUpdaterProject = Join-Path $repositoryRoot 'updater\Osiris.Updater.csproj'
$osirisUpdaterOutput = Join-Path $repositoryRoot 'updater\bin\Release\net462\Osiris.Updater.exe'
$osirisLauncherProject = Join-Path $repositoryRoot 'src\Osiris.Launcher\Osiris.Launcher.csproj'
$osirisLauncherOutput = Join-Path $repositoryRoot 'src\Osiris.Launcher\bin\Release\net462\Osiris.exe'
$largeAddressAwareTool = Join-Path $repositoryRoot 'build\Set-LargeAddressAware.ps1'

if (-not (Test-Path -LiteralPath (Join-Path $sourceRootPath 'Osiris.exe') -PathType Leaf) -or
    -not (Test-Path -LiteralPath $sourceApp -PathType Container)) {
    throw "SourceRoot is not a valid Osiris installation: $sourceRootPath"
}

if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "Version metadata is missing: $versionFile"
}

$sourceCoreAssembly = Join-Path $sourceApp 'Playnite.dll'
if (-not (Test-Path -LiteralPath $sourceCoreAssembly -PathType Leaf)) {
    throw "The inherited core assembly is missing: $sourceCoreAssembly"
}

if (-not (Test-Path -LiteralPath $programUpdateTool -PathType Leaf)) {
    throw "The inherited program-update verification tool is missing: $programUpdateTool"
}

if (-not (Test-Path -LiteralPath $protocolHookTool -PathType Leaf)) {
    throw "The inherited protocol-handler verification tool is missing: $protocolHookTool"
}

if (-not (Test-Path -LiteralPath $startupSplashTool -PathType Leaf)) {
    throw "The inherited startup-splash verification tool is missing: $startupSplashTool"
}

if (-not (Test-Path -LiteralPath $osirisUpdaterProject -PathType Leaf)) {
    throw "The Osiris updater project is missing: $osirisUpdaterProject"
}

if (-not (Test-Path -LiteralPath $osirisLauncherProject -PathType Leaf)) {
    throw "The Osiris launcher project is missing: $osirisLauncherProject"
}

if (-not (Test-Path -LiteralPath $largeAddressAwareTool -PathType Leaf)) {
    throw "The LARGE_ADDRESS_AWARE build tool is missing: $largeAddressAwareTool"
}

& dotnet build $osirisLauncherProject --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $osirisLauncherOutput -PathType Leaf)) {
    throw 'Release validation failed; the Osiris launcher could not be built.'
}

& dotnet build $osirisUpdaterProject --configuration Release --verbosity minimal
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $osirisUpdaterOutput -PathType Leaf)) {
    throw 'Release validation failed; the Osiris updater could not be built.'
}

& dotnet run --project $programUpdateTool --configuration Release -- --verify $sourceCoreAssembly
if ($LASTEXITCODE -ne 0) {
    throw 'Release validation failed; inherited program updates are not disabled.'
}

$versionMetadata = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
$version = [string]$versionMetadata.version
if ($version -cnotmatch '^(Alpha|Beta|Stable)_\d+\.\d+\.\d+$') {
    throw "Invalid Osiris version in version.json: $version"
}
$versionStage = $version.Split('_')[0].ToLowerInvariant()
if ($versionStage -ne ([string]$versionMetadata.channel).ToLowerInvariant()) {
    throw "Osiris version stage '$versionStage' does not match channel '$($versionMetadata.channel)'."
}

if ($outputRoot.StartsWith($sourceRootPath + '\', [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($outputRoot, $sourceRootPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output directory cannot be inside the working Osiris installation.'
}

$packageName = "Osiris-$version-win-x64"
$stagingParent = Join-Path $outputRoot 'staging'
$packageRoot = Join-Path $stagingParent $packageName
$archivePath = Join-Path $outputRoot ($packageName + '.zip')
$manifestPath = Join-Path $outputRoot ($packageName + '.json')

Assert-ChildPath -Parent $outputRoot -Child $stagingParent
Assert-ChildPath -Parent $outputRoot -Child $packageRoot
Assert-ChildPath -Parent $outputRoot -Child $archivePath
Assert-ChildPath -Parent $outputRoot -Child $manifestPath

foreach ($existingPath in @($packageRoot, $archivePath, $manifestPath)) {
    if (Test-Path -LiteralPath $existingPath) {
        if (-not $Force) {
            throw "Release output already exists: $existingPath. Use -Force to rebuild it."
        }

        Remove-Item -LiteralPath $existingPath -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$destinationApp = Join-Path $packageRoot 'App'

$excludedAppDirectories = @(
    'Backup', 'Backups', 'browsercache', 'cache', 'config', 'Data',
    'Extensions', 'ExtensionsData', 'library', 'logs', 'Programming', 'Recovery', 'Runtime',
    'Settings', 'User Data'
)
$excludedAppFiles = @(
    'backup.json', 'config.json', 'fullscreenConfig.json', 'libraryState.ini',
    'osiris-font-size.json', 'osiris-game-details-settings.json',
    'osiris-grid-view-settings.json', 'osiris-ui-settings.json',
    'osiris-ui-theme.json', 'windowPositions.json', '*.bak', '*.tmp', '*.log', '*.pdb', '*~'
)

$excludedDirectoryPaths = @($excludedAppDirectories | ForEach-Object {
    Join-Path $sourceApp $_
})
$robocopyArguments = @(
    $sourceApp,
    $destinationApp,
    '/E', '/COPY:DAT', '/DCOPY:DAT', '/R:2', '/W:1',
    '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
    '/XD'
) + $excludedDirectoryPaths + @('/XF') + $excludedAppFiles

& robocopy @robocopyArguments
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -gt 7) {
    throw "Robocopy failed with exit code $robocopyExitCode."
}

$stagedDesktopEngine = Join-Path $destinationApp 'Osiris.DesktopEngine.exe'
if (-not (Test-Path -LiteralPath $stagedDesktopEngine -PathType Leaf)) {
    throw 'Release validation failed; App/Osiris.DesktopEngine.exe is missing.'
}
& $largeAddressAwareTool -Path $stagedDesktopEngine | Out-Null
& $largeAddressAwareTool -Path $stagedDesktopEngine -VerifyOnly | Out-Null
& dotnet run --project $startupSplashTool --configuration Release -- --verify $stagedDesktopEngine
if ($LASTEXITCODE -ne 0) {
    throw 'Release validation failed; the inherited startup splash is still enabled.'
}

$stagedFullscreenEngine = Join-Path $destinationApp 'Osiris.FullscreenEngine.exe'
if (-not (Test-Path -LiteralPath $stagedFullscreenEngine -PathType Leaf)) {
    throw 'Release validation failed; App/Osiris.FullscreenEngine.exe is missing.'
}

$stagedCoreAssembly = Join-Path $destinationApp 'Playnite.dll'
if (-not (Test-Path -LiteralPath $stagedCoreAssembly -PathType Leaf)) {
    throw 'Release validation failed; the inherited core assembly is missing.'
}

& dotnet run --project $protocolHookTool --configuration Release -- --verify $stagedCoreAssembly
if ($LASTEXITCODE -ne 0) {
    throw 'Release validation failed; inherited protocol and file-association hooks are still enabled.'
}

Copy-Item -LiteralPath $osirisUpdaterOutput -Destination (Join-Path $destinationApp 'Osiris.Updater.exe') -Force

$commonConfigPath = Join-Path $destinationApp 'Common.config'
if (-not (Test-Path -LiteralPath $commonConfigPath -PathType Leaf)) {
    throw 'Release validation failed; App/Common.config is missing.'
}
[xml]$commonConfig = Get-Content -LiteralPath $commonConfigPath -Raw
foreach ($setting in @{
    UpdateUrl = 'about:blank'
    UpdateUrl2 = 'about:blank'
    UpdateBranch = 'disabled'
}.GetEnumerator()) {
    $node = $commonConfig.appSettings.add |
        Where-Object { $_.key -eq $setting.Key } |
        Select-Object -First 1
    if ($null -eq $node) {
        throw "Release validation failed; Common.config is missing $($setting.Key)."
    }
    $node.value = $setting.Value
}
$commonConfig.Save($commonConfigPath)

$themeMainWindowPath = Join-Path $destinationApp 'Themes\Desktop\Default\Views\MainWindow.xaml'
if (-not (Test-Path -LiteralPath $themeMainWindowPath -PathType Leaf)) {
    throw 'Release validation failed; the Osiris desktop theme MainWindow.xaml is missing.'
}
$themeMainWindow = [xml]::new()
$themeMainWindow.PreserveWhitespace = $true
$themeMainWindow.Load($themeMainWindowPath)
$namespaces = [Xml.XmlNamespaceManager]::new($themeMainWindow.NameTable)
$namespaces.AddNamespace('wpf', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$namespaces.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
$footerLabels = @($themeMainWindow.SelectNodes(
    '//wpf:StackPanel[wpf:Button[@x:Name="PART_OsirisDonateButton"]]/wpf:TextBlock', $namespaces))
$footerStageNodes = @($footerLabels | Where-Object { $_.GetAttribute('Text') -match '^(Alpha|Beta|Stable)$' })
$footerNumberNodes = @($footerLabels | Where-Object { $_.GetAttribute('Text') -match '^v?\d+\.\d+\.\d+$' })
if ($footerStageNodes.Count -ne 1 -or $footerNumberNodes.Count -ne 1) {
    throw "Release validation failed; expected one styled Osiris footer version label."
}
$footerStage = $version.Split('_')[0]
if ($footerStageNodes[0].GetAttribute('Text') -cmatch '^[A-Z]+$') {
    $footerStage = $footerStage.ToUpperInvariant()
}
$footerNumber = $version.Substring($version.IndexOf('_') + 1)
if ($footerNumberNodes[0].GetAttribute('Text').StartsWith('v')) { $footerNumber = 'v' + $footerNumber }
$footerStageNodes[0].SetAttribute('Text', $footerStage)
$footerNumberNodes[0].SetAttribute('Text', $footerNumber)

# Welcome-on-every-startup is a Development-only testing switch.
# Change the staged theme, never the reusable Development installation/profile.
$welcomeOverlay = $themeMainWindow.SelectSingleNode(
    '//wpf:Grid[@x:Name="PART_OsirisWelcomeOverlay"]', $namespaces)
if ($null -eq $welcomeOverlay) {
    throw 'Release validation failed; the welcome screen is missing.'
}
$welcomeStartupSwitch = $welcomeOverlay.GetAttributeNode(
    'WelcomeNotificationController.ShowOnEveryStartup',
    'clr-namespace:OsirisTheme;assembly=OsirisTheme')
if ($null -eq $welcomeStartupSwitch) {
    throw 'Release validation failed; the welcome startup policy is missing.'
}
$welcomeStartupSwitch.Value = 'False'
$themeMainWindow.Save($themeMainWindowPath)

foreach ($fileName in @('Osiris.exe', 'Uninstall Osiris.exe')) {
    $sourceFile = Join-Path $sourceRootPath $fileName
    if (Test-Path -LiteralPath $sourceFile -PathType Leaf) {
        Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $packageRoot $fileName)
    }
}

Copy-Item -LiteralPath $osirisLauncherOutput -Destination (Join-Path $packageRoot 'Osiris.exe') -Force

Copy-Item -LiteralPath $versionFile -Destination (Join-Path $packageRoot 'version.json')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE')
# Use the public installation guide, not a Development README with local machine paths.
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\INSTALL.md') -Destination (Join-Path $packageRoot 'README.txt')

New-Item -ItemType Directory -Path (Join-Path $packageRoot 'Data') | Out-Null

foreach ($entry in $excludedAppDirectories + $excludedAppFiles) {
    if (Test-Path -LiteralPath (Join-Path $destinationApp $entry)) {
        throw "Release validation failed; writable state was copied into App: $entry"
    }
}

$stagedDataItems = @(Get-ChildItem -LiteralPath (Join-Path $packageRoot 'Data') -Force)
if ($stagedDataItems.Count -ne 0) {
    throw 'Release validation failed; the staged Data directory is not empty.'
}

$nestedState = @(Get-ChildItem -LiteralPath $destinationApp -Directory -Recurse |
    Where-Object { $_.Name -in @('Data', 'ExtensionsData', 'library', 'logs', 'Recovery', 'browsercache') })
if ($nestedState.Count -ne 0) {
    throw "Release validation failed; nested writable state was copied into App: $($nestedState.FullName -join ', ')"
}

if (-not (Test-Path -LiteralPath (Join-Path $destinationApp 'license.txt') -PathType Leaf)) {
    throw 'Release validation failed; App/license.txt is missing.'
}

$stagedCommonConfig = Get-Content -LiteralPath $commonConfigPath -Raw
if ($stagedCommonConfig -match 'playnite\.link/update' -or
    $stagedCommonConfig -notmatch 'key="UpdateBranch" value="disabled"') {
    throw 'Release validation failed; the inherited update channel is still configured.'
}

$stagedThemeMainWindow = Get-Content -LiteralPath $themeMainWindowPath -Raw
if ($stagedThemeMainWindow -notmatch ('Text="' + [regex]::Escape($footerStage) + '"') -or
    $stagedThemeMainWindow -notmatch ('Text="' + [regex]::Escape($footerNumber) + '"')) {
    throw 'Release validation failed; the desktop footer version does not match version.json.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $packageRoot,
    $archivePath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false
)

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$archiveInfo = Get-Item -LiteralPath $archivePath
$manifest = [ordered]@{
    schemaVersion = 1
    version = $version
    channel = [string]$versionMetadata.channel
    publishedAtUtc = [DateTime]::UtcNow.ToString('o')
    asset = $archiveInfo.Name
    size = $archiveInfo.Length
    sha256 = $archiveHash
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

Write-Output "Release package: $archivePath"
Write-Output "Release manifest: $manifestPath"
Write-Output "SHA-256: $archiveHash"
