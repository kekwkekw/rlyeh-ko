# Rlyeh Korean installer. Copyright (c) 2026 kekwkekw. AGPL-3.0-only.
# Windows PowerShell 5.1 and PowerShell 7. No download, game launch or global setting change.
[CmdletBinding()]
param(
    [ValidateSet('VerifyPackage','Plan','Install','Restore')][string]$Mode = 'VerifyPackage',
    [string]$GameRoot,
    [string]$Receipt
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
$PackageRoot = $PSScriptRoot

function Safe-Path([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
        $Relative.Contains(':') -or ($Relative -split '[\\/]' | Where-Object { $_ -in @('..','.','') })) {
        throw "Unsafe relative path: $Relative"
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $full = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (!$full.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes root' }
    for ($part = $full; $part; $part = Split-Path $part -Parent) {
        $item = Get-Item -LiteralPath $part -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked paths are not supported: $part" }
        $parent = Split-Path $part -Parent
        if ($parent -eq $part) { break }
    }
    return $full
}
function Hash([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
    if (Test-Path -LiteralPath $Path) { throw "Expected a file: $Path" }
    return $null
}
function Allowed([string]$Path) {
    return $Path -match '^(BepInEx/core/[^/]+|dotnet/[^/]+|BepInEx/plugins/Rlyeh\.Localizer/(Rlyeh\.Localizer(\.Core)?\.dll|glossary/[^/]+|fonts/[^/]+|native/hdTransEngine\.dll|native/hdtrans-data/dicts\.hdsnap|images/catalog\.json|images/files/[a-f0-9]{64}\.png|scenario/reviewed\.json)|winhttp\.dll|doorstop_config\.ini|\.doorstop_version)$'
}
function Atomic-Copy([string]$Source, [string]$Target, [string]$Expected) {
    if ((Hash $Source) -ne $Expected) { throw "Source changed: $Source" }
    [IO.Directory]::CreateDirectory((Split-Path $Target -Parent)) | Out-Null
    $temp = $Target + '.rlyeh-' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::Copy($Source, $temp)
        if ((Hash $temp) -ne $Expected) { throw "Copy verification failed: $Target" }
        if ([IO.File]::Exists($Target)) { [IO.File]::Replace($temp, $Target, [NullString]::Value) }
        else { [IO.File]::Move($temp, $Target) }
    } finally { if ([IO.File]::Exists($temp)) { Remove-Item -LiteralPath $temp -Force } }
}
function Save-Receipt($Value, [string]$Path) {
    $temp = $Path + '.tmp'
    [IO.File]::WriteAllText($temp, ($Value | ConvertTo-Json -Depth 7), (New-Object Text.UTF8Encoding($false)))
    if ([IO.File]::Exists($Path)) { [IO.File]::Replace($temp, $Path, [NullString]::Value) }
    else { [IO.File]::Move($temp, $Path) }
}
function Find-GameRoots {
    $candidates = New-Object System.Collections.Generic.List[string]
    $seen = @{}
    $add = {
        param([string]$Dir)
        if ([string]::IsNullOrWhiteSpace($Dir)) { return }
        try {
            if (!(Test-Path -LiteralPath $Dir -PathType Container)) { return }
            $full = (Resolve-Path -LiteralPath $Dir).Path.TrimEnd('\')
            if ($seen.ContainsKey($full.ToLowerInvariant())) { return }
            if (Test-Path -LiteralPath (Join-Path $full 'rlyehshoujotaix_cl.exe') -PathType Leaf) {
                $seen[$full.ToLowerInvariant()] = $true
                $candidates.Add($full) | Out-Null
            }
        } catch {}
    }
    if ($env:APPDATA) {
        $dmmCfg = Join-Path $env:APPDATA 'dmmgameplayer5\dmmgame.cnf'
        if (Test-Path -LiteralPath $dmmCfg -PathType Leaf) {
            try {
                $cfg = Get-Content -LiteralPath $dmmCfg -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($item in @($cfg.contents)) {
                    if ($item -and $item.PSObject.Properties['detail'] -and $item.detail -and $item.detail.PSObject.Properties['path']) {
                        & $add ([string]$item.detail.path)
                    }
                }
            } catch {}
        }
    }
    if ($env:USERPROFILE) { & $add (Join-Path $env:USERPROFILE 'rlyehshoujotaix_cl') }
    foreach ($root in @('C:\Games','D:\Games','E:\Games','C:\DMMGamePlayer','D:\DMMGamePlayer','C:\','D:\','E:\')) {
        & $add (Join-Path $root 'rlyehshoujotaix_cl')
    }
    if ($env:ProgramFiles) { & $add (Join-Path $env:ProgramFiles 'DMMGamePlayer\Games\rlyehshoujotaix_cl') }
    if (${env:ProgramFiles(x86)}) { & $add (Join-Path ${env:ProgramFiles(x86)} 'DMMGamePlayer\Games\rlyehshoujotaix_cl') }
    return @($candidates.ToArray())
}
function Open-FolderDialog([string]$InitialPath) {
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne [Threading.ApartmentState]::STA) { return $null }
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $owner = New-Object System.Windows.Forms.Form
        $owner.TopMost = $true
        $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
        $dlg.Description = 'Select the game folder containing rlyehshoujotaix_cl.exe'
        $dlg.ShowNewFolderButton = $false
        if ($InitialPath -and (Test-Path -LiteralPath $InitialPath -PathType Container)) { $dlg.SelectedPath = $InitialPath }
        try {
            if ($dlg.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) { return $dlg.SelectedPath }
        } finally { $dlg.Dispose(); $owner.Dispose() }
    } catch {}
    return $null
}
function Open-ReceiptDialog([string]$InitialDir) {
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne [Threading.ApartmentState]::STA) { return $null }
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $owner = New-Object System.Windows.Forms.Form
        $owner.TopMost = $true
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Title = 'Select backup receipt (manifest.json)'
        $dlg.Filter = 'Backup receipt (manifest.json)|manifest.json|JSON files (*.json)|*.json'
        $dlg.CheckFileExists = $true
        if ($InitialDir -and (Test-Path -LiteralPath $InitialDir -PathType Container)) { $dlg.InitialDirectory = $InitialDir }
        try {
            if ($dlg.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) { return $dlg.FileName }
        } finally { $dlg.Dispose(); $owner.Dispose() }
    } catch {}
    return $null
}
function Select-GameRoot {
    $found = @(Find-GameRoots)
    if ($found.Count -gt 0) {
        Write-Host 'Detected game folder(s):'
        for ($i = 0; $i -lt $found.Count; $i++) { Write-Host ("  [{0}] {1}" -f ($i + 1), $found[$i]) }
        $answer = (Read-Host 'Press Enter for [1], enter a number, type B to open folder picker, or paste a folder path').Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($answer)) { return $found[0] }
        if ($answer -match '^\d+$') {
            $idx = [int]$answer - 1
            if ($idx -ge 0 -and $idx -lt $found.Count) { return $found[$idx] }
            throw "Invalid selection: $answer"
        }
        if ($answer -ieq 'B') {
            $picked = Open-FolderDialog $found[0]
            if (!$picked) { throw 'Folder selection cancelled' }
            return $picked
        }
        return $answer
    }
    $answer = (Read-Host 'Game folder containing rlyehshoujotaix_cl.exe (press Enter or B to open folder picker, or paste path)').Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($answer) -or $answer -ieq 'B') {
        $picked = Open-FolderDialog ''
        if (!$picked) {
            $answer = (Read-Host 'Paste the game folder path containing rlyehshoujotaix_cl.exe').Trim().Trim('"')
            if ([string]::IsNullOrWhiteSpace($answer)) { throw 'No game folder provided' }
            return $answer
        }
        return $picked
    }
    return $answer
}
function Select-RestoreReceipt([string]$Game) {
    $backupRoot = Safe-Path $Game 'RlyehPatchBackups'
    $all = @()
    if (Test-Path -LiteralPath $backupRoot -PathType Container) {
        foreach ($dir in @(Get-ChildItem -LiteralPath $backupRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            $mp = Join-Path $dir.FullName 'manifest.json'
            if (!(Test-Path -LiteralPath $mp -PathType Leaf)) { continue }
            try {
                $rec = Get-Content -LiteralPath $mp -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($rec.schema -eq 1) {
                    $all += [pscustomobject]@{ id=$dir.Name; path=$mp; package=$rec.package; status=$rec.status; count=@($rec.files).Count }
                }
            } catch {}
        }
    }
    $active = @($all | Where-Object { $_.status -ne 'restored' })
    $list = @(if ($active.Count -gt 0) { $active } else { $all })
    if ($list.Count -gt 0) {
        Write-Host 'Available backup receipts (newest first):'
        for ($i = 0; $i -lt $list.Count; $i++) {
            $r = $list[$i]
            Write-Host ("  [{0}] {1}  (package {2}, {3} files, status: {4})" -f ($i + 1), $r.id, $r.package, $r.count, $r.status)
        }
        $answer = (Read-Host 'Press Enter for [1], enter a number, type B to browse for manifest.json, or paste a path').Trim().Trim('"')
        $chosen = $null
        if ([string]::IsNullOrWhiteSpace($answer)) { $chosen = $list[0].path }
        elseif ($answer -match '^\d+$') {
            $idx = [int]$answer - 1
            if ($idx -lt 0 -or $idx -ge $list.Count) { throw "Invalid selection: $answer" }
            $chosen = $list[$idx].path
        } elseif ($answer -ieq 'B') {
            $chosen = Open-ReceiptDialog $backupRoot
            if (!$chosen) { throw 'Receipt selection cancelled' }
        } else { $chosen = $answer }
        $confirm = (Read-Host ("Restore using {0}? Type Y to continue" -f $chosen)).Trim()
        if ($confirm -ine 'Y') { throw 'Restore cancelled by user' }
        return $chosen
    }
    Write-Host "No backup receipts found under: $backupRoot"
    $answer = (Read-Host 'Press Enter or B to browse for manifest.json, or paste its path').Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($answer) -or $answer -ieq 'B') {
        $picked = Open-ReceiptDialog $backupRoot
        if (!$picked) { throw 'Restore requires -Receipt <manifest.json>' }
        return $picked
    }
    return $answer
}
function Verify-Package {
    $manifest = Get-Content -LiteralPath (Join-Path $PackageRoot 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.schema -ne 1) { throw 'Unsupported package schema' }
    $seen = @{}
    foreach ($file in $manifest.files) {
        if ($seen.ContainsKey($file.path)) { throw "Duplicate package path: $($file.path)" }
        $seen[$file.path] = $true
        $path = Safe-Path $PackageRoot $file.path
        if ((Hash $path) -ne $file.sha256) { throw "Package checksum mismatch: $($file.path)" }
        if ((Get-Item -LiteralPath $path).Length -ne $file.size) { throw "Package size mismatch: $($file.path)" }
        if ($file.path.StartsWith('payload/') -and !(Allowed $file.path.Substring(8))) { throw "Unexpected payload path: $($file.path)" }
    }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PackageRoot 'payload') -File -Recurse -Force) {
        $relative = $file.FullName.Substring($PackageRoot.Length + 1).Replace('\','/')
        if (!$seen.ContainsKey($relative)) { throw "Unlisted payload file: $relative" }
    }
    Write-Host "Package verified: $($manifest.version), $(@($manifest.files).Count) files"
    return $manifest
}

if ($Mode -eq 'Restore') {
    if (!$Receipt) {
        if (!$GameRoot) { $GameRoot = Select-GameRoot }
        $GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path.TrimEnd('\')
        if (!(Test-Path -LiteralPath (Safe-Path $GameRoot 'rlyehshoujotaix_cl.exe') -PathType Leaf)) { throw 'Wrong game folder' }
        $Receipt = Select-RestoreReceipt $GameRoot
    }
    $Receipt = (Resolve-Path -LiteralPath $Receipt).Path
    $record = Get-Content -LiteralPath $Receipt -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($record.schema -ne 1) { throw 'Unsupported recovery receipt' }
    if (!$GameRoot) { $GameRoot = $record.gameRoot }
} else { $manifest = Verify-Package }
if ($Mode -eq 'VerifyPackage') { return }
if (!$GameRoot) { $GameRoot = Select-GameRoot }
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path.TrimEnd('\')
if (!(Test-Path -LiteralPath (Safe-Path $GameRoot 'rlyehshoujotaix_cl.exe') -PathType Leaf)) { throw 'Wrong game folder' }
if ($Mode -ne 'Plan' -and (Get-Process -Name 'rlyehshoujotaix_cl' -ErrorAction SilentlyContinue)) { throw 'Close the game first' }

if ($Mode -eq 'Restore') {
    if ($GameRoot -ne $record.gameRoot) { throw 'Receipt belongs to a different game folder' }
    $backup = Split-Path $Receipt -Parent
    $backupRoot = Safe-Path $GameRoot 'RlyehPatchBackups'
    if (!$Receipt.StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Receipt must stay in its original backup folder' }
    $seen = @{}
    foreach ($entry in $record.files) {
        if (!(Allowed $entry.path) -or $seen.ContainsKey($entry.path)) { throw 'Invalid recovery path' }
        $seen[$entry.path] = $true
        $current = Hash (Safe-Path $GameRoot $entry.path)
        if ($current -ne $entry.before -and $current -ne $entry.after) { throw "File changed since installation; preserve it before recovery: $($entry.path)" }
        if ($entry.before -and (Hash (Safe-Path $backup ('before/' + $entry.path))) -ne $entry.before) { throw "Invalid backup: $($entry.path)" }
    }
    $lock = [IO.File]::Open((Safe-Path $GameRoot 'RlyehPatchBackups/operation.lock'), 'OpenOrCreate','ReadWrite','None')
    try {
        foreach ($entry in $record.files) {
            $target = Safe-Path $GameRoot $entry.path
            $current = Hash $target
            if ($current -eq $entry.before) { continue }
            if ($current -ne $entry.after) { throw "File changed during recovery: $($entry.path)" }
            if ($entry.before) { Atomic-Copy (Safe-Path $backup ('before/' + $entry.path)) $target $entry.before }
            else { Remove-Item -LiteralPath $target -Force }
        }
        $record.status = 'restored'
        Save-Receipt $record $Receipt
        Write-Host 'Restored original files. Generated cache, config, interop and logs are retained.'
    } finally { $lock.Dispose() }
    return
}

# Do not combine this plugin with legacy translators or unknown patchers.
foreach ($folder in @('BepInEx/plugins','BepInEx/patchers')) {
    $path = Safe-Path $GameRoot $folder
    if (Test-Path -LiteralPath $path) {
        foreach ($dll in Get-ChildItem -LiteralPath $path -Filter '*.dll' -Recurse -File) {
            $relative = $dll.FullName.Substring($GameRoot.Length + 1).Replace('\','/')
            if ($relative -notin @('BepInEx/plugins/Rlyeh.Localizer/Rlyeh.Localizer.dll','BepInEx/plugins/Rlyeh.Localizer/Rlyeh.Localizer.Core.dll','BepInEx/plugins/Rlyeh.Localizer/native/hdTransEngine.dll')) {
                throw "Other plugin found. Back up/remove the old patch before installing: $relative"
            }
        }
    }
}
$config = Safe-Path $GameRoot 'BepInEx/config/local.rlyeh.korean.cfg'
if (Test-Path -LiteralPath $config) {
    $content = Get-Content -LiteralPath $config -Raw -Encoding UTF8
    if ($content -match '(?m)^[ \t]*Dictionary[ \t]*=[ \t]*[^ \t\r\n]') {
        Write-Warning 'Existing Dictionary override is preserved. Clear its value to use the bundled dictionary.'
    }
}
$files = @($manifest.files | Where-Object {$_.path.StartsWith('payload/')} | ForEach-Object {
    $relative = $_.path.Substring(8)
    [pscustomobject]@{path=$relative;before=(Hash (Safe-Path $GameRoot $relative));after=$_.sha256}
})
$changed = @($files | Where-Object {$_.before -ne $_.after})
Write-Host "Plan: $($files.Count) managed files; $($changed.Count) will change."
if ($Mode -eq 'Plan' -or !$changed.Count) { return }
$backupRoot = Safe-Path $GameRoot 'RlyehPatchBackups'
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$lock = [IO.File]::Open((Safe-Path $GameRoot 'RlyehPatchBackups/operation.lock'), 'OpenOrCreate','ReadWrite','None')
try {
    # Recheck after acquiring the lock; another installer must not invalidate our plan.
    foreach ($entry in $files) { if ((Hash (Safe-Path $GameRoot $entry.path)) -ne $entry.before) { throw 'Game files changed during planning' } }
    $id = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
    $backup = Safe-Path $backupRoot $id
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    foreach ($entry in $changed) {
        if ($entry.before) { Atomic-Copy (Safe-Path $GameRoot $entry.path) (Safe-Path $backup ('before/' + $entry.path)) $entry.before }
    }
    $Receipt = Safe-Path $backup 'manifest.json'
    $record = [pscustomobject]@{schema=1;package=$manifest.version;gameRoot=$GameRoot;status='installing';files=$changed}
    Save-Receipt $record $Receipt
    Write-Host "Recovery receipt: $Receipt"
    foreach ($entry in $changed) {
        $target = Safe-Path $GameRoot $entry.path
        if ((Hash $target) -ne $entry.before) { throw "File changed during installation: $($entry.path)" }
        Atomic-Copy (Safe-Path $PackageRoot ('payload/' + $entry.path)) $target $entry.after
    }
    foreach ($entry in $files) { if ((Hash (Safe-Path $GameRoot $entry.path)) -ne $entry.after) { throw 'Installed hash mismatch' } }
    $record.status = 'installed'
    Save-Receipt $record $Receipt
    Write-Host 'Installed. Start the game with your normal DMM launcher.'
} finally { $lock.Dispose() }
