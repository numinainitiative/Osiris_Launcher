[CmdletBinding()]
param(
    [string]$ArtifactDirectory,
    [string]$ReleaseNotesFile,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $PSScriptRoot '..\artifacts'
}
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionMetadata = Get-Content -LiteralPath (Join-Path $repositoryRoot 'version.json') -Raw | ConvertFrom-Json
$version = [string]$versionMetadata.version
$displayVersion = $version.Replace('_', ' ')
$owner = [string]$versionMetadata.repositoryOwner
$repository = [string]$versionMetadata.repositoryName
$tag = $version
$packageName = "Osiris-$version-win-x64.zip"
$manifestName = "Osiris-$version-win-x64.json"
$packagePath = Join-Path $ArtifactDirectory $packageName
$manifestPath = Join-Path $ArtifactDirectory $manifestName
$token = [Environment]::GetEnvironmentVariable('OSIRIS_GITHUB_TOKEN')

if ([string]::IsNullOrWhiteSpace($token)) {
    throw 'Set OSIRIS_GITHUB_TOKEN to a GitHub token with Contents: write permission.'
}
foreach ($asset in @($packagePath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        throw "Release asset is missing: $asset"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packageInfo = Get-Item -LiteralPath $packagePath
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ([string]$manifest.version -ne $version -or
    [string]$manifest.asset -ne $packageName -or
    [long]$manifest.size -ne $packageInfo.Length -or
    [string]$manifest.sha256 -ne $packageHash) {
    throw 'Release manifest validation failed.'
}

$headers = @{
    Accept = 'application/vnd.github+json'
    Authorization = "Bearer $token"
    'X-GitHub-Api-Version' = '2026-03-10'
    'User-Agent' = 'Osiris-Release-Publisher/0.1'
}
$apiRoot = "https://api.github.com/repos/$owner/$repository"

$existingRelease = $null
try {
    $existingRelease = Invoke-RestMethod -Uri "$apiRoot/releases/tags/$tag" -Headers $headers
}
catch {
    if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) {
        throw
    }
}
if ($null -ne $existingRelease) {
    throw "GitHub release $tag already exists. Refusing to replace published assets."
}

$notes = if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesFile)) {
    [IO.File]::ReadAllText([IO.Path]::GetFullPath($ReleaseNotesFile))
}
else {
    "Osiris $version"
}
$isPrerelease = [string]$versionMetadata.channel -ne 'stable'
$payload = [ordered]@{
    tag_name = $tag
    target_commitish = 'main'
    name = "Osiris $displayVersion"
    body = $notes
    draft = -not $Publish
    prerelease = $isPrerelease
    generate_release_notes = $false
} | ConvertTo-Json

$release = Invoke-RestMethod -Method Post -Uri "$apiRoot/releases" -Headers $headers `
    -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
$uploadRoot = ([string]$release.upload_url).Split('{')[0]

foreach ($asset in @(
    @{ Path = $packagePath; ContentType = 'application/zip' },
    @{ Path = $manifestPath; ContentType = 'application/json' }
)) {
    $name = [Uri]::EscapeDataString((Split-Path -Leaf $asset.Path))
    Invoke-RestMethod -Method Post -Uri ($uploadRoot + '?name=' + $name) -Headers $headers `
        -ContentType $asset.ContentType -InFile $asset.Path | Out-Null
}

Write-Output "Created GitHub release: $($release.html_url)"
Write-Output "State: $(if ($Publish) { 'published' } else { 'draft' })"
Write-Output "Uploaded: $packageName"
Write-Output "Uploaded: $manifestName"
