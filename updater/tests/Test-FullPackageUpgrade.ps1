[CmdletBinding()]
param(
    [string]$ReleaseOutput
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ([string]::IsNullOrWhiteSpace($ReleaseOutput)) {
    $ReleaseOutput = Join-Path $PSScriptRoot '..\..\artifacts\github-updater-release-verification'
}
$ReleaseOutput = [IO.Path]::GetFullPath($ReleaseOutput)

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$upgradeRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot 'Programming\Sandbox\UpgradeTest'))
$testRoot = [IO.Path]::GetFullPath((Join-Path $upgradeRoot 'GitHubUpdater-Beta_1.0.32'))
if (-not $testRoot.StartsWith($upgradeRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe upgrade-test path.'
}
if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot | Out-Null

$baseVersion = 'Beta_1.0.31'
$installedOldVersion = 'Beta_1.0.31'
$targetVersion = 'Beta_1.0.32'
$baseArchive = Join-Path $ReleaseOutput "Osiris-$baseVersion-win-x64.zip"
$installRoot = Join-Path $testRoot 'installation'
[IO.Compression.ZipFile]::ExtractToDirectory($baseArchive, $installRoot)

$installedVersionFile = Join-Path $installRoot 'version.json'
$installedVersionData = Get-Content -LiteralPath $installedVersionFile -Raw | ConvertFrom-Json
$installedVersionData.version = $installedOldVersion
$installedVersionData | ConvertTo-Json | Set-Content -LiteralPath $installedVersionFile -Encoding utf8

$sentinel = Join-Path $installRoot 'Data\preserve-me.txt'
Set-Content -LiteralPath $sentinel -Value 'full package user data sentinel'
$sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash

$assetName = "Osiris-$targetVersion-win-x64.zip"
$manifestName = "Osiris-$targetVersion-win-x64.json"
$targetArchive = Join-Path $ReleaseOutput $assetName
$manifestPath = Join-Path $ReleaseOutput $manifestName

$releaseApi = Join-Path $testRoot 'releases.json'
$releaseList = @([ordered]@{
    tag_name = $targetVersion
    body = 'Full sanitized Osiris package upgrade test.'
    draft = $false
    prerelease = $true
    assets = @(
        [ordered]@{ name = $manifestName; browser_download_url = ([Uri]$manifestPath).AbsoluteUri },
        [ordered]@{ name = $assetName; browser_download_url = ([Uri]$targetArchive).AbsoluteUri }
    )
})
ConvertTo-Json -InputObject $releaseList -Depth 8 |
    Set-Content -LiteralPath $releaseApi -Encoding utf8

$updater = Join-Path $installRoot 'App\Osiris.Updater.exe'
$process = Start-Process -FilePath $updater -ArgumentList @(
    '--check', '--root', ('"' + $installRoot + '"'), '--parent', '0',
    '--api', ('"' + ([Uri]$releaseApi).AbsoluteUri + '"'), '--accept', '--no-restart'
) -Wait -PassThru
if ($process.ExitCode -ne 10) {
    throw "The full-package update checker returned $($process.ExitCode)."
}

$log = Join-Path $installRoot 'Data\logs\osiris-updater.log'
for ($attempt = 0; $attempt -lt 600; $attempt++) {
    if ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -SimpleMatch "Installed Osiris $targetVersion" -Quiet)) {
        break
    }
    Start-Sleep -Milliseconds 100
}
if (-not (Test-Path -LiteralPath $log) -or
    -not (Select-String -LiteralPath $log -SimpleMatch "Installed Osiris $targetVersion" -Quiet)) {
    throw 'The full-package update did not finish.'
}

$installedVersion = (Get-Content -LiteralPath (Join-Path $installRoot 'version.json') -Raw | ConvertFrom-Json).version
if ($installedVersion -ne $targetVersion) { throw "Installed version is $installedVersion." }
if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) {
    throw 'The full-package update modified Data.'
}

$backupApps = @(Get-ChildItem -LiteralPath (Join-Path $installRoot 'Data\Recovery\Updates') `
    -Recurse -Directory -Filter App)
if ($backupApps.Count -lt 1) { throw 'The updater did not preserve a rollback App backup.' }

Write-Output "Full package upgrade passed: $installedOldVersion -> $targetVersion"
Write-Output 'Data sentinel preserved and rollback backup created.'
