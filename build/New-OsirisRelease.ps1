[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$OutputDirectory,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $PSScriptRoot '..\..\Sandboxes\Development\Osiris'
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

if (-not (Test-Path -LiteralPath (Join-Path $sourceRootPath 'Osiris.exe') -PathType Leaf) -or
    -not (Test-Path -LiteralPath $sourceApp -PathType Container)) {
    throw "SourceRoot is not a valid Osiris installation: $sourceRootPath"
}

if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "Version metadata is missing: $versionFile"
}

$versionMetadata = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
$version = [string]$versionMetadata.version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid semantic version in version.json: $version"
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
    'Extensions', 'ExtensionsData', 'library', 'logs', 'Recovery', 'Runtime',
    'Settings', 'User Data'
)
$excludedAppFiles = @(
    'backup.json', 'config.json', 'fullscreenConfig.json', 'libraryState.ini',
    'osiris-font-size.json', 'osiris-game-details-settings.json',
    'osiris-grid-view-settings.json', 'osiris-ui-settings.json',
    'osiris-ui-theme.json', 'windowPositions.json'
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

foreach ($fileName in @('Osiris.exe', 'README.txt', 'Uninstall Osiris.exe')) {
    $sourceFile = Join-Path $sourceRootPath $fileName
    if (Test-Path -LiteralPath $sourceFile -PathType Leaf) {
        Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $packageRoot $fileName)
    }
}

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

if (-not (Test-Path -LiteralPath (Join-Path $destinationApp 'license.txt') -PathType Leaf)) {
    throw 'Release validation failed; App/license.txt is missing.'
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
