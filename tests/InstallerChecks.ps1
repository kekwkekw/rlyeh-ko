# Exercise real package manifests and recovery receipts in disposable fixture folders.
# This does not run the game or use actual game files.
param(
    [Parameter(Mandatory)][string]$CurrentPackage,
    [Parameter(Mandatory)][string]$PreviousPackage,
    [Parameter(Mandatory)][string]$Root
)
$ErrorActionPreference = 'Stop'
$CurrentPackage = (Resolve-Path -LiteralPath $CurrentPackage).Path
$PreviousPackage = (Resolve-Path -LiteralPath $PreviousPackage).Path
$Root = [IO.Path]::GetFullPath($Root)
if (Test-Path -LiteralPath $Root) { throw 'Use a new fixture directory' }
[IO.Directory]::CreateDirectory($Root) | Out-Null
$shell = (Get-Process -Id $PID).Path
$current = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $CurrentPackage 'package.json') | ConvertFrom-Json
$previous = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $PreviousPackage 'package.json') | ConvertFrom-Json
$currentFiles = @($current.files | Where-Object { $_.path.StartsWith('payload/') })
$previousFiles = @($previous.files | Where-Object { $_.path.StartsWith('payload/') })

function Hash([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
    return $null
}
function Run([string]$Package, [string]$Mode, [string]$Game, [string]$Receipt, [switch]$Fails) {
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $Package 'Install.ps1'),'-Mode',$Mode)
    if ($Game) { $arguments += @('-GameRoot',$Game) }
    if ($Receipt) { $arguments += @('-Receipt',$Receipt) }
    $lines = & $shell @arguments 2>&1
    $code = $LASTEXITCODE
    $output = $lines | Out-String
    Write-Host $output
    if (($code -eq 0) -eq [bool]$Fails) { throw "Unexpected installer exit code: $Mode / $code" }
    return $output
}
function Receipt([string]$Output) {
    $path = [regex]::Match($Output,'(?m)^Recovery receipt: ([^\r\n]+)').Groups[1].Value
    if (!$path) { throw 'Missing recovery receipt' }
    return $path
}
function Check-Files([string]$Game, $Files) {
    foreach ($file in $Files) {
        if ((Hash (Join-Path $Game $file.path.Substring(8))) -ne $file.sha256) { throw "Installed bytes differ: $($file.path)" }
    }
}
function Fixture([string]$Name) {
    $game = Join-Path $Root $Name
    [IO.Directory]::CreateDirectory($game) | Out-Null
    [IO.File]::WriteAllText((Join-Path $game 'rlyehshoujotaix_cl.exe'), 'Non-executable installer fixture')
    return $game
}
function User-Data([string]$Game) {
    $config = Join-Path $Game 'BepInEx/config/local.rlyeh.korean.cfg'
    $cache = Join-Path $Game 'BepInEx/plugins/Rlyeh.Localizer/cache/user-test.jsonl'
    foreach ($path in @($config,$cache)) { [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null }
    [IO.File]::WriteAllText($config, "[General]`r`nEnabled = false`r`n[Local]`r`nDictionary =`r`n")
    [IO.File]::WriteAllText($cache, '["preserved-source","preserved-translation"]')
    return @(@{path=$config;sha=(Hash $config)},@{path=$cache;sha=(Hash $cache)})
}
function Check-User($Files) {
    foreach ($file in $Files) { if ((Hash $file.path) -ne $file.sha) { throw 'Changed user configuration/cache' } }
}

Run $CurrentPackage VerifyPackage | Out-Null
$game = Fixture '업그레이드 시험'
$original = Join-Path $game 'doorstop_config.ini'
[IO.File]::WriteAllText($original, 'Original loader configuration')
$originalHash = Hash $original
$exeHash = Hash (Join-Path $game 'rlyehshoujotaix_cl.exe')
$oldReceipt = Receipt (Run $PreviousPackage Install $game)
$user = User-Data $game
$upgradeReceipt = Receipt (Run $CurrentPackage Install $game)
Check-Files $game $currentFiles
Check-User $user
$receiptHash = Hash $upgradeReceipt
$again = Run $CurrentPackage Install $game
if ($again -notmatch '0 will change' -or $again -match 'Recovery receipt:' -or (Hash $upgradeReceipt) -ne $receiptHash) {
    throw 'Identical reinstall changed files or recovery history'
}
Run $CurrentPackage Restore $game $upgradeReceipt | Out-Null
Check-Files $game $previousFiles
Check-User $user
$oldPaths = @($previousFiles | ForEach-Object { $_.path })
foreach ($file in $currentFiles) {
    if ($file.path -notin $oldPaths -and (Test-Path -LiteralPath (Join-Path $game $file.path.Substring(8)))) {
        throw 'Upgrade restore retained a newly added managed file'
    }
}
$secondReceipt = Receipt (Run $CurrentPackage Install $game)
$image = $currentFiles | Where-Object { $_.path -match '/images/files/.+\.png$' } | Select-Object -First 1
if (!$image) { throw 'Package must exercise installed image recovery' }
$imagePath = Join-Path $game $image.path.Substring(8)
[IO.File]::AppendAllText($imagePath,'User modification')
$refusal = Run $CurrentPackage Restore $game $secondReceipt -Fails
if ($refusal -notmatch 'File changed since installation') { throw 'Recovery did not explain its refusal' }
$plugin = $currentFiles | Where-Object { $_.path -eq 'payload/BepInEx/plugins/Rlyeh.Localizer/Rlyeh.Localizer.dll' }
if ((Hash (Join-Path $game $plugin.path.Substring(8))) -ne $plugin.sha256) { throw 'Refused recovery partially restored the plugin' }
Copy-Item -LiteralPath (Join-Path $CurrentPackage $image.path) -Destination $imagePath
Run $CurrentPackage Restore $game $secondReceipt | Out-Null
Check-Files $game $previousFiles
Run $CurrentPackage Restore $game $oldReceipt | Out-Null
if ((Hash $original) -ne $originalHash -or (Hash (Join-Path $game 'rlyehshoujotaix_cl.exe')) -ne $exeHash) { throw 'Original fixture files changed' }
Check-User $user

$fresh = Fixture '신규 설치 시험'
$freshReceipt = Receipt (Run $CurrentPackage Install $fresh)
Check-Files $fresh $currentFiles
$freshUser = User-Data $fresh
Run $CurrentPackage Restore $fresh $freshReceipt | Out-Null
foreach ($file in $currentFiles) {
    if (Test-Path -LiteralPath (Join-Path $fresh $file.path.Substring(8))) { throw 'Fresh restore retained a managed file' }
}
Check-User $freshUser
Run $CurrentPackage Install $fresh | Out-Null
Check-Files $fresh $currentFiles
Check-User $freshUser
Write-Host "PASS: $($previous.version) -> $($current.version), fresh install, no-op reinstall, recovery, edited-image refusal and preserved user bytes ($($currentFiles.Count) files)"
