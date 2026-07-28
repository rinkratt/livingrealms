param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseName,

    [Parameter(Mandatory = $true)]
    [string[]]$Notes
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$exportDirectory = Join-Path $repositoryRoot 'artifacts\playtest\windows'
$releaseDirectoryName = '{0}-{1}-{2}' -f $ReleaseName, $Version, (Get-Date -Format 'yyyyMMdd')
$releaseDirectory = Join-Path $repositoryRoot "artifacts\deploy\$releaseDirectoryName"
$stageDirectory = Join-Path $releaseDirectory 'windows'
$zipName = "LivingRealms-Playtest-Windows-$Version.zip"
$zipPath = Join-Path $releaseDirectory $zipName
$partialZipPath = "$zipPath.partial"
$manifestDirectory = Join-Path $releaseDirectory 'site\downloads'
$manifestPath = Join-Path $manifestDirectory 'windows-version.json'
$checksumPath = Join-Path $releaseDirectory 'LivingRealms-Playtest-Windows.sha256.txt'

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$resolvedRelease = [IO.Path]::GetFullPath($releaseDirectory)
if (-not $resolvedRelease.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release path escaped the artifacts directory: $resolvedRelease"
}

$requiredExportFiles = @(
    (Join-Path $exportDirectory 'LivingRealms.exe'),
    (Join-Path $exportDirectory 'LivingRealms.pck'),
    (Join-Path $exportDirectory 'data_LivingRealms.Client_windows_x86_64')
)
foreach ($required in $requiredExportFiles) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing exported game component: $required"
    }
}

if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $exportDirectory 'LivingRealms.exe') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $exportDirectory 'LivingRealms.pck') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $exportDirectory 'data_LivingRealms.Client_windows_x86_64') -Destination $stageDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'client\LivingRealms.Client\LivingRealms.Updater.ps1') -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\PLAYTEST-PACKAGE-README.txt') -Destination (Join-Path $stageDirectory 'README.txt')

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $partialZipPath) {
    Remove-Item -LiteralPath $partialZipPath -Force
}
[IO.Compression.ZipFile]::CreateFromDirectory(
    $stageDirectory,
    $partialZipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false
)
Move-Item -LiteralPath $partialZipPath -Destination $zipPath

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entryNames = $archive.Entries.FullName
    foreach ($requiredEntry in @('LivingRealms.exe', 'LivingRealms.pck', 'LivingRealms.Updater.ps1', 'README.txt')) {
        if ($requiredEntry -notin $entryNames) {
            throw "The ZIP is missing $requiredEntry"
        }
    }
    if ($archive.Entries.Count -lt 100) {
        throw "The ZIP contains too few files: $($archive.Entries.Count)"
    }
}
finally {
    $archive.Dispose()
}

$zipItem = Get-Item -LiteralPath $zipPath
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
"$hash  $zipName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

$manifest = [ordered]@{
    version = $Version
    minimumVersion = '0.8.3'
    downloadUrl = "https://living-realms.com/downloads/$zipName"
    sha256 = $hash
    sizeBytes = $zipItem.Length
    publishedAt = [DateTimeOffset]::UtcNow.ToString('O')
    notes = $Notes
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText($manifestPath, $manifestJson, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Version = $Version
    Package = $zipPath
    SizeBytes = $zipItem.Length
    Sha256 = $hash
    Entries = $entryNames.Count
    Manifest = $manifestPath
}
