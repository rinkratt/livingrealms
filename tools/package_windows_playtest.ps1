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
$verificationDirectory = Join-Path $releaseDirectory 'zip-full-extraction-test'

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

# A valid central directory alone does not prove that Windows can extract every
# compressed payload. Perform a complete extraction, compare every staged file
# byte-for-byte by SHA-256, and verify the executable/package signatures before
# a release is allowed to publish.
if (Test-Path -LiteralPath $verificationDirectory) {
    Remove-Item -LiteralPath $verificationDirectory -Recurse -Force
}
[IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $verificationDirectory)

$stageFiles = Get-ChildItem -LiteralPath $stageDirectory -File -Recurse
$extractedFiles = Get-ChildItem -LiteralPath $verificationDirectory -File -Recurse
$stageRootWithSeparator = $stageDirectory.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar
) + [IO.Path]::DirectorySeparatorChar
if ($stageFiles.Count -ne $extractedFiles.Count) {
    throw "Full extraction produced $($extractedFiles.Count) files; $($stageFiles.Count) were staged."
}
foreach ($sourceFile in $stageFiles) {
    if (-not $sourceFile.FullName.StartsWith($stageRootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Staged file escaped the expected Windows package directory: $($sourceFile.FullName)"
    }
    $relativePath = $sourceFile.FullName.Substring($stageRootWithSeparator.Length)
    $extractedPath = Join-Path $verificationDirectory $relativePath
    if (-not (Test-Path -LiteralPath $extractedPath -PathType Leaf)) {
        throw "Full extraction is missing $relativePath"
    }
    $extractedFile = Get-Item -LiteralPath $extractedPath
    if ($sourceFile.Length -ne $extractedFile.Length) {
        throw "Extracted size mismatch for $relativePath"
    }
    $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
    $extractedHash = (Get-FileHash -LiteralPath $extractedPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $extractedHash) {
        throw "Extracted content mismatch for $relativePath"
    }
}

$verifiedExe = Join-Path $verificationDirectory 'LivingRealms.exe'
$exeStream = [IO.File]::OpenRead($verifiedExe)
try {
    if ($exeStream.ReadByte() -ne 0x4D -or $exeStream.ReadByte() -ne 0x5A) {
        throw 'The extracted LivingRealms.exe does not have a valid Windows executable signature.'
    }
}
finally {
    $exeStream.Dispose()
}
$verifiedPck = Get-Item -LiteralPath (Join-Path $verificationDirectory 'LivingRealms.pck')
if ($verifiedPck.Length -lt 1MB) {
    throw "The extracted LivingRealms.pck is unexpectedly small: $($verifiedPck.Length) bytes"
}
Remove-Item -LiteralPath $verificationDirectory -Recurse -Force

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
    ExtractedFilesVerified = $stageFiles.Count
    Manifest = $manifestPath
}
