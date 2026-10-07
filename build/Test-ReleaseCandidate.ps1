[CmdletBinding()]
param([string]$WorkspaceRoot = 'C:\Development\Osiris Launcher', [string]$Version = 'Beta_0.0.51', [string]$PreviousVersion = 'Beta_0.0.50')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd('\')
$artifacts = Join-Path $repositoryRoot 'artifacts'
$archive = Join-Path $artifacts "Osiris-$Version-win-x64.zip"
$manifestPath = Join-Path $artifacts "Osiris-$Version-win-x64.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sha256) { throw 'Candidate checksum mismatch.' }
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '^(Data[/\\].+)|[/\\](ExtensionsData|library|logs|Recovery|browsercache)[/\\]' -or $entry.FullName -match '\.(log|pdb)$') { throw "Private/debug file in archive: $($entry.FullName)" }
    }
} finally { $zip.Dispose() }
$qualificationId = "$Version-qualification-" + [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$cleanRoot = Join-Path $workspace "Programming\Sandbox\CleanInstall\$qualificationId"
$upgradeRoot = Join-Path $workspace "Programming\Sandbox\UpgradeTest\$qualificationId"
foreach ($target in @($cleanRoot, $upgradeRoot)) {
    if (-not $target.StartsWith($workspace + '\Programming\Sandbox\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $target)) { throw 'Unsafe or existing qualification directory.' }
    New-Item -ItemType Directory -Path $target | Out-Null
}
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $cleanRoot)
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $artifacts "Osiris-$PreviousVersion-win-x64.zip"), $upgradeRoot)
$extensions = @(
    @('Stats','0.1.3','Stats_511681f5-d2f5-41ae-8b73-fa3adcc86c85','Extras','Stats'),
    @('ScreenshotsGallery','0.1.0','ScreenshotsGallery_f3dc3fd5-3d6d-4aa0-8762-2d325bb1d7fe','Extras','Screenshots Gallery'),
    @('GameGallery','2.0.4','GameGallery_8e77fe31-5e62-41e2-8fa2-64844cfd5b6b','Extras','Game Gallery'),
    @('Trophies','0.1.0','Trophies_72ba1fc9-490b-4fa4-96f0-0a154b56c1db','Extras','Trophies'),
    @('Exophase','0.2.7','Exophase_26131977-669a-4ef7-a66c-026122a24089','Extras','Exophase'),
    @('SteamLibrary','1.0.3','SteamLibrary_cb91dfc9-b977-43bf-8e70-55f46e410fab','Libraries','Steam Library'),
    @('XboxLibrary','1.0.2','XboxLibrary_7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287','Libraries','Xbox Library')
)
foreach ($extension in $extensions) {
    $package = Join-Path $workspace "GitHub\Extensions\artifacts\$($extension[0])\$($extension[1])\$($extension[0])_$($extension[1]).pext"
    $checksum = Get-Content -LiteralPath "$package.sha256.json" -Raw | ConvertFrom-Json
    if ($checksum.version -ne $extension[1] -or $checksum.size -ne (Get-Item $package).Length -or
        $checksum.sha256 -ne (Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant()) { throw "Extension fixture checksum mismatch: $($extension[0])" }
    $extensionRoot = Join-Path $cleanRoot "Data\Extensions\$($extension[3])\$($extension[2])"
    New-Item -ItemType Directory -Path $extensionRoot -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $extensionRoot)
}
# Keep an old Stats installation through the application update.
$oldStatsRoot = Join-Path $upgradeRoot 'Data\Extensions\Extras\Stats_511681f5-d2f5-41ae-8b73-fa3adcc86c85'
New-Item -ItemType Directory -Path $oldStatsRoot -Force | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $workspace 'GitHub\Extensions\artifacts\Stats\0.1.2\Stats_0.1.2.pext'), $oldStatsRoot)
$oldStatsHash = (Get-FileHash -LiteralPath (Join-Path $oldStatsRoot 'Osiris.Stats.dll')).Hash
$sentinel = Join-Path $upgradeRoot 'Data\qualification-sentinel.txt'
'Disposable qualification data; must survive update and rollback.' | Set-Content -LiteralPath $sentinel
$sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
$apiPath = Join-Path $upgradeRoot 'qualification-releases.json'
$release = @(@{ tag_name=$Version; draft=$false; prerelease=$false; body='Local release qualification'; assets=@(@{name=[IO.Path]::GetFileName($manifestPath);browser_download_url=([Uri]$manifestPath).AbsoluteUri},@{name=[IO.Path]::GetFileName($archive);browser_download_url=([Uri]$archive).AbsoluteUri}) })
ConvertTo-Json -InputObject $release -Depth 8 | Set-Content -LiteralPath $apiPath
$updater = Start-Process -FilePath (Join-Path $upgradeRoot 'App\Osiris.Updater.exe') -ArgumentList @('--check','--root',('"'+$upgradeRoot+'"'),'--api',('"'+([Uri]$apiPath).AbsoluteUri+'"'),'--accept','--no-restart') -WindowStyle Hidden -Wait -PassThru
if ($updater.ExitCode -ne 10) { throw "Unexpected update check exit code $($updater.ExitCode)" }
$updateLog = Join-Path $upgradeRoot 'Data\logs\osiris-updater.log'
for ($attempt=0; $attempt -lt 300; $attempt++) {
    if ((Test-Path $updateLog) -and (Select-String -LiteralPath $updateLog -SimpleMatch "Installed Osiris $Version" -Quiet)) { break }
    Start-Sleep -Milliseconds 100
}
if ((Get-Content -LiteralPath (Join-Path $upgradeRoot 'version.json') -Raw | ConvertFrom-Json).version -ne $Version) { throw 'Upgrade did not install candidate.' }
if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash -or (Get-FileHash -LiteralPath (Join-Path $oldStatsRoot 'Osiris.Stats.dll')).Hash -ne $oldStatsHash) { throw 'Upgrade modified existing Data.' }

function Test-Startup([string]$Root, [switch]$VerifyExtensions) {
    $engines = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Osiris.DesktopEngine.exe' })
    if ($engines.Count -ne 0) { throw 'Another Osiris engine is active; refusing shared-IPC test.' }
    $wrapper = Start-Process -FilePath (Join-Path $Root 'Osiris.exe') -ArgumentList '--skipupdatecheck' -WindowStyle Hidden -PassThru
    try {
        $log = Join-Path $Root 'Data\logs\playnite.log'
        for ($attempt=0; $attempt -lt 250; $attempt++) {
            if ((Test-Path $log) -and (Select-String -LiteralPath $log -Pattern 'Application [0-9.]+ started' -Quiet)) { break }
            Start-Sleep -Milliseconds 100
        }
        $engine = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Osiris.DesktopEngine.exe' })
        if ($engine.Count -ne 1 -or $engine[0].ExecutablePath -ne (Join-Path $Root 'App\Osiris.DesktopEngine.exe') -or -not $engine[0].CommandLine.Contains((Join-Path $Root 'Data'))) { throw 'Wrong startup installation or Data path.' }
        if (-not (Test-Path $log)) { throw 'Startup log missing.' }
        if (Select-String -LiteralPath $log -Pattern 'FATAL|Unhandled|Error loading plugin|Exception.*Xaml|Failed to load.*extension' -Quiet) { throw "Startup errors: $log" }
        if (Select-String -LiteralPath $log -SimpleMatch 'G:\Gaming\Apps\Osiris Launcher App' -Quiet) { throw 'Personal path leaked into startup.' }
        if ($VerifyExtensions) {
            foreach ($extension in $extensions) {
                if (-not (Select-String -LiteralPath $log -SimpleMatch "Loaded plugin: $($extension[4]), version $($extension[1])" -Quiet)) {
                    throw "Coordinated extension did not load: $($extension[4]) $($extension[1])"
                }
            }
        }
        Write-Output "Startup validated: $Root"
    } finally {
        $engine = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Osiris.DesktopEngine.exe' })
        if ($engine.Count -eq 1 -and $engine[0].ExecutablePath -eq (Join-Path $Root 'App\Osiris.DesktopEngine.exe')) {
            Start-Process -FilePath (Join-Path $Root 'Osiris.exe') -ArgumentList '--skipupdatecheck --shutdown' -WindowStyle Hidden -Wait
            for ($attempt=0; $attempt -lt 100; $attempt++) { if (-not (Get-Process -Id $engine[0].ProcessId -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 100 }
            for ($attempt=0; $attempt -lt 100; $attempt++) { if (-not (Get-Process -Id $wrapper.Id -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 100 }
            if (Get-Process -Id $engine[0].ProcessId -ErrorAction SilentlyContinue) { throw 'Sandbox engine did not shut down.' }
            if (Get-Process -Id $wrapper.Id -ErrorAction SilentlyContinue) { throw 'Sandbox wrapper did not shut down.' }
        }
    }
}
Test-Startup $cleanRoot -VerifyExtensions
Test-Startup $upgradeRoot
Write-Output "Candidate qualified; clean=$cleanRoot; upgrade=$upgradeRoot; checksum=$($manifest.sha256); Data sentinel and old Stats preserved."
