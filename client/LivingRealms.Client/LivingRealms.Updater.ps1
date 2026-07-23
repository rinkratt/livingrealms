param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$InstallDirectory,

    [Parameter(Mandatory = $true)]
    [int]$WaitForProcessId,

    [Parameter(Mandatory = $true)]
    [string]$ExecutableName,

    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'

function Write-UpdateLog {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -LiteralPath $LogPath -Value "$timestamp  $Message"
}

$stagingDirectory = $null
$backupDirectory = $null

try {
    $resolvedPackage = [System.IO.Path]::GetFullPath($PackagePath)
    $resolvedInstall = [System.IO.Path]::GetFullPath($InstallDirectory)
    $resolvedLog = [System.IO.Path]::GetFullPath($LogPath)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())

    $logDirectory = Split-Path -Parent $resolvedLog
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Write-UpdateLog 'Automatic update started.'

    if (-not (Test-Path -LiteralPath $resolvedPackage -PathType Leaf)) {
        throw 'The downloaded update package is missing.'
    }
    if (-not (Test-Path -LiteralPath $resolvedInstall -PathType Container)) {
        throw 'The Living Realms installation folder is missing.'
    }
    $installedExecutable = Join-Path $resolvedInstall $ExecutableName
    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
        throw 'The selected folder is not a Living Realms installation.'
    }

    if ($WaitForProcessId -gt 0) {
        Write-UpdateLog "Waiting for game process $WaitForProcessId to close."
        Wait-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue
    }

    $updateId = [Guid]::NewGuid().ToString('N')
    $stagingDirectory = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot "LivingRealms-Update-$updateId"))
    $backupDirectory = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot "LivingRealms-Backup-$updateId"))
    if (-not $stagingDirectory.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $backupDirectory.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The updater could not create safe temporary folders.'
    }

    New-Item -ItemType Directory -Path $stagingDirectory,$backupDirectory | Out-Null
    Expand-Archive -LiteralPath $resolvedPackage -DestinationPath $stagingDirectory -Force

    $stagedExecutable = Join-Path $stagingDirectory $ExecutableName
    $stagedPack = Join-Path $stagingDirectory 'LivingRealms.pck'
    $stagedData = Get-ChildItem -LiteralPath $stagingDirectory -Directory |
        Where-Object { $_.Name -like 'data_LivingRealms*_windows_x86_64' } |
        Select-Object -First 1
    if (-not (Test-Path -LiteralPath $stagedExecutable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $stagedPack -PathType Leaf) -or
        $null -eq $stagedData) {
        throw 'The downloaded archive is not a complete Living Realms build.'
    }

    Write-UpdateLog 'Creating rollback backup.'
    Get-ChildItem -LiteralPath $stagingDirectory -Force | ForEach-Object {
        $existingPath = Join-Path $resolvedInstall $_.Name
        if (Test-Path -LiteralPath $existingPath) {
            Copy-Item -LiteralPath $existingPath -Destination $backupDirectory -Recurse -Force
        }
    }

    try {
        Write-UpdateLog 'Installing verified package.'
        Get-ChildItem -LiteralPath $stagingDirectory -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $resolvedInstall -Recurse -Force
        }
    }
    catch {
        Write-UpdateLog "Installation failed; restoring backup: $($_.Exception.Message)"
        Get-ChildItem -LiteralPath $backupDirectory -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $resolvedInstall -Recurse -Force
        }
        throw
    }

    Write-UpdateLog 'Automatic update completed successfully.'
    if (-not $NoRestart) {
        Start-Process -FilePath $installedExecutable -WorkingDirectory $resolvedInstall
    }
}
catch {
    try {
        Write-UpdateLog "Automatic update failed: $($_.Exception.Message)"
    }
    catch {
    }
    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show(
            "Living Realms could not install its update.`n`n$($_.Exception.Message)`n`nDetails: $LogPath",
            'Living Realms Update',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    }
    catch {
    }
    exit 1
}
finally {
    foreach ($temporaryDirectory in @($stagingDirectory,$backupDirectory)) {
        if ([string]::IsNullOrWhiteSpace($temporaryDirectory)) {
            continue
        }
        $resolvedTemporary = [System.IO.Path]::GetFullPath($temporaryDirectory)
        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($resolvedTemporary.StartsWith($temporaryRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $resolvedTemporary -PathType Container)) {
            Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
        }
    }
}
