[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testRoot = Join-Path $repositoryRoot 'artifacts\updater-integration-test'
$launcher = Join-Path $repositoryRoot 'src\Osiris.Launcher\bin\Release\net462\Osiris.exe'
$updater = Join-Path $repositoryRoot 'updater\bin\Release\net462\Osiris.Updater.exe'

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Write-VersionFile {
    param([string]$Root, [string]$Version)

    [ordered]@{
        version = $Version
        channel = 'beta'
        repositoryOwner = 'numinainitiative'
        repositoryName = 'Osiris_Launcher'
        releaseApiUrl = 'overridden-by-test'
        checkForUpdates = $true
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Root 'version.json') -Encoding utf8
}

function New-TestInstallation {
    param([string]$Root, [string]$Version, [string]$Marker)

    New-Item -ItemType Directory -Path (Join-Path $Root 'App') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Root 'Data') -Force | Out-Null
    Copy-Item -LiteralPath $launcher -Destination (Join-Path $Root 'Osiris.exe')
    Copy-Item -LiteralPath $updater -Destination (Join-Path $Root 'App\Osiris.Updater.exe')
    Set-Content -LiteralPath (Join-Path $Root 'App\release-marker.txt') -Value $Marker
    Write-VersionFile -Root $Root -Version $Version
}

function New-TestRelease {
    param(
        [string]$Version,
        [string]$Marker,
        [switch]$ForceApplyFailure
    )

    $releaseRoot = Join-Path $testRoot ("release-" + $Version)
    $packageRoot = Join-Path $releaseRoot 'package'
    New-TestInstallation -Root $packageRoot -Version $Version -Marker $Marker
    if ($ForceApplyFailure) {
        Set-Content -LiteralPath (Join-Path $packageRoot 'blocked.txt') -Value 'This must collide with a directory.'
    }

    $assetName = "Osiris-$Version-win-x64.zip"
    $manifestName = "Osiris-$Version-win-x64.json"
    $archive = Join-Path $releaseRoot $assetName
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $archive)
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $archiveInfo = Get-Item -LiteralPath $archive
    [ordered]@{
        schemaVersion = 1
        version = $Version
        channel = 'beta'
        asset = $assetName
        size = $archiveInfo.Length
        sha256 = $hash
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot $manifestName) -Encoding utf8

    $releaseApi = Join-Path $releaseRoot 'releases.json'
    $releaseList = @([ordered]@{
        tag_name = $Version
        body = "Automated updater integration test for $Version."
        draft = $false
        prerelease = $true
        assets = @(
            [ordered]@{
                name = $manifestName
                browser_download_url = ([Uri](Join-Path $releaseRoot $manifestName)).AbsoluteUri
            },
            [ordered]@{
                name = $assetName
                browser_download_url = ([Uri]$archive).AbsoluteUri
            }
        )
    })
    ConvertTo-Json -InputObject $releaseList -Depth 8 |
        Set-Content -LiteralPath $releaseApi -Encoding utf8

    return $releaseApi
}

function Invoke-UpdateCheck {
    param([string]$InstallRoot, [string]$ReleaseApi)

    $process = Start-Process -FilePath (Join-Path $InstallRoot 'App\Osiris.Updater.exe') `
        -ArgumentList @(
            '--check', '--root', ('"' + $InstallRoot + '"'),
            '--parent', '0', '--api', ('"' + ([Uri]$ReleaseApi).AbsoluteUri + '"'),
            '--accept', '--no-restart'
        ) `
        -Wait -PassThru
    if ($process.ExitCode -ne 10) {
        throw "The update checker returned $($process.ExitCode), expected 10."
    }
}

function Wait-ForLogText {
    param([string]$InstallRoot, [string]$Text)

    $log = Join-Path $InstallRoot 'Data\logs\osiris-updater.log'
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        if ((Test-Path -LiteralPath $log) -and
            (Select-String -LiteralPath $log -SimpleMatch $Text -Quiet)) {
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for updater log text: $Text"
}

$installRoot = Join-Path $testRoot 'installation'
New-TestInstallation -Root $installRoot -Version 'Beta_2026.0.31' -Marker 'old'
$sentinel = Join-Path $installRoot 'Data\preserve-me.txt'
Set-Content -LiteralPath $sentinel -Value 'personal data must survive'
$sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash

$successApi = New-TestRelease -Version 'Beta_1.0.31' -Marker 'updated'
Invoke-UpdateCheck -InstallRoot $installRoot -ReleaseApi $successApi
Wait-ForLogText -InstallRoot $installRoot -Text 'Installed Osiris Beta_1.0.31'

$installedVersion = (Get-Content -LiteralPath (Join-Path $installRoot 'version.json') -Raw | ConvertFrom-Json).version
if ($installedVersion -ne 'Beta_1.0.31') { throw "Unexpected installed version: $installedVersion" }
if ((Get-Content -LiteralPath (Join-Path $installRoot 'App\release-marker.txt') -Raw).Trim() -ne 'updated') {
    throw 'The new App payload was not installed.'
}
if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) {
    throw 'Data was modified during a successful update.'
}

New-Item -ItemType Directory -Path (Join-Path $installRoot 'blocked.txt') | Out-Null
$failureApi = New-TestRelease -Version 'Beta_1.0.32' -Marker 'must-rollback' -ForceApplyFailure
Invoke-UpdateCheck -InstallRoot $installRoot -ReleaseApi $failureApi
Wait-ForLogText -InstallRoot $installRoot -Text 'Rollback completed.'

$rolledBackVersion = (Get-Content -LiteralPath (Join-Path $installRoot 'version.json') -Raw | ConvertFrom-Json).version
if ($rolledBackVersion -ne 'Beta_1.0.31') { throw "Rollback left version $rolledBackVersion" }
if ((Get-Content -LiteralPath (Join-Path $installRoot 'App\release-marker.txt') -Raw).Trim() -ne 'updated') {
    throw 'Rollback did not restore the previous App payload.'
}
if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) {
    throw 'Data was modified during rollback.'
}

Write-Output 'Updater integration test passed:'
Write-Output '  legacy Beta_2026.0.31 -> conventional Beta_1.0.31 installed'
Write-Output '  Data sentinel preserved'
Write-Output '  forced Beta_1.0.32 failure rolled back to Beta_1.0.31'
