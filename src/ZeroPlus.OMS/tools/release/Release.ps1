#Requires -Version 5.1

<#
.SYNOPSIS
Builds, packages, and ships a ZeroPlus OMS release to the network share, then
makes an empty release commit and pushes an annotated tag. Use -DryRun for a
local-only test build.

.DESCRIPTION
Reads the current version from \\zpapp01.corp.zeroplusderivatives.com\oms\releases.win.json,
bumps it per -ReleaseType, builds the OMS UI with Velopack delta packaging,
uploads to the share, then creates and pushes the release commit + tag.

The upload step only pushes files that vpk pack created or modified -
the prior nupkg/manifest pulled in by vpk download just to seed delta
computation is left alone on the share.

In -DryRun mode the git sync, vpk download, upload, and commit/tag steps
are all skipped; the manifest is still read so the build is stamped with
the same version a real release would produce.

.PARAMETER ReleaseType
Semantic version increment: BugFix (patch), Minor, or Major. Default: BugFix.

.PARAMETER BuildConfiguration
.NET build configuration. Default: Release.

.PARAMETER ReleaseNotesPath
Path to the release notes markdown file. Defaults to ReleaseNotes.md in the
repo root. Its contents become the body of the release commit message
(below a "Release vX.Y.Z" title) and are also passed to vpk pack.

.PARAMETER DryRun
Local-only test mode. Reads the share manifest and computes the next
version exactly like a real release, but skips: git sync (no reset --hard,
no clean -fd), the vpk download for delta computation, share upload, and
the commit + tag step. Artifacts land under
%TEMP%\ZeroPlusOMSRelease\<runId>\velopack-out\. Useful for testing the
script or the build itself without touching production state.

.PARAMETER PackPortable
When set, Velopack also emits the portable bundle (*-Portable.zip).
By default --noPortable is passed so only Setup.exe / nupkg artifacts are produced.

.EXAMPLE
.\tools\release\Release.ps1 -ReleaseType Minor

.EXAMPLE
.\tools\release\Release.ps1 -DryRun

.EXAMPLE
.\tools\release\Release.ps1 -PackPortable
#>

[CmdletBinding()]
param(
    [ValidateSet('BugFix', 'Minor', 'Major')]
    [string]$ReleaseType = 'BugFix',

    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfiguration = 'Release',

    [string]$ReleaseNotesPath,

    [switch]$DryRun,

    [switch]$PackPortable
)

$ErrorActionPreference = 'Stop'

# Make sure Unicode output renders in classic conhost where the default
# OEM codepage would otherwise turn box-drawing characters into '?'.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# --- constants --------------------------------------------------------------

$RepoRoot        = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$SharedDrivePath = '\\zpapp01.corp.zeroplusderivatives.com\oms'
$PackageId       = 'ZeroPlus-OMS-UI'
$PackTitle       = 'ZeroPlus OMS'
$MainExe         = 'ZeroPlus OMS.exe'
$VpkFramework    = 'net8-x64-desktop'
$VpkChannel      = 'win'

# Identity stamped on the release commit and annotated tag. Scoped to the
# commit/tag step via env vars so the developer's global git config is
# untouched for every other command.
$ReleaseCommitterName  = 'OMS Builder'
$ReleaseCommitterEmail = 'oms@zeroplusderivatives.com'

$UiCsproj        = Join-Path $RepoRoot 'ZeroPlus.Oms.Ui\ZeroPlus.Oms.Ui.csproj'
$UiPubxml        = Join-Path $RepoRoot 'ZeroPlus.Oms.Ui\Properties\PublishProfiles\FolderProfile.pubxml'
$UiBuildOutput   = Join-Path $RepoRoot 'ZeroPlus.Oms.Ui\bin\Release\net8.0-windows\win-x64'

$KsCsproj        = Join-Path $RepoRoot 'ZeroPlus.OMS.KillSwitch\ZeroPlus.OMS.KillSwitch.csproj'
$KsPubxml        = Join-Path $RepoRoot 'ZeroPlus.OMS.KillSwitch\Properties\PublishProfiles\FolderProfile.pubxml'

$IconPath        = Join-Path $RepoRoot 'zpicon.ico'

if (-not $ReleaseNotesPath) { $ReleaseNotesPath = Join-Path $RepoRoot 'ReleaseNotes.md' }

# --- per-run paths ----------------------------------------------------------

$RunId        = Get-Date -Format 'yyyyMMdd-HHmmss'
$RunDir       = Join-Path $env:TEMP "ZeroPlusOMSRelease\$RunId"
$CombinedLog  = Join-Path $RunDir 'release.log'
$StagingDir   = Join-Path $RunDir 'velopack-out'
# Snapshot of $StagingDir taken after `vpk download` but before `vpk pack`.
# The upload step uses it to skip files that came from the share unchanged
# (old nupkgs pulled in just to seed delta computation).
$PreSnapshot  = Join-Path $RunDir 'pre-pack-snapshot.json'

# --- UI primitives ----------------------------------------------------------

# Spinner only animates in environments where cursor manipulation is reliable.
# ISE has no real console; CI logs work better with simple line-by-line output.
$UseSpinner    = ($Host.Name -ne 'Windows PowerShell ISE Host') -and (-not $env:CI)
$SpinnerFrames = @([char]0x280B, [char]0x2819, [char]0x2839, [char]0x2838,
                   [char]0x283C, [char]0x2834, [char]0x2826, [char]0x2827,
                   [char]0x2807, [char]0x280F)
$MarkOk        = [char]0x2714  # heavy check
$MarkFail      = [char]0x2718  # heavy ballot x

function Get-LineWidth {
    try { return [Math]::Max(80, $Host.UI.RawUI.WindowSize.Width) } catch { return 100 }
}

function Write-StepLine {
    [CmdletBinding()]
    param(
        [string]$Mark,
        [ConsoleColor]$MarkColor,
        [string]$Label,
        [Nullable[double]]$Seconds,
        [string]$Info,
        [switch]$Final
    )

    $w = Get-LineWidth
    [Console]::Write("`r" + (' ' * ($w - 1)) + "`r")

    [Console]::ForegroundColor = $MarkColor
    [Console]::Write(("  {0} " -f $Mark))
    [Console]::ResetColor()

    if ($null -ne $Seconds) {
        [Console]::Write(("{0,-38} {1,7}" -f $Label, ('{0:N1}s' -f $Seconds)))
        if ($Info) {
            [Console]::ForegroundColor = 'DarkGray'
            [Console]::Write("   $Info")
            [Console]::ResetColor()
        }
    } else {
        [Console]::ForegroundColor = 'Gray'
        [Console]::Write($Label)
        [Console]::ResetColor()
    }

    if ($Final) { [Console]::WriteLine() }
}

function Format-Elapsed {
    param([TimeSpan]$Span)
    if ($Span.TotalMinutes -ge 1) {
        '{0}m {1:N0}s' -f [int]$Span.TotalMinutes, $Span.Seconds
    } else {
        '{0:N1}s' -f $Span.TotalSeconds
    }
}

# --- the core step runner ---------------------------------------------------
#
# Each step's $Action is a self-contained scriptblock that:
#   - takes ($LogFile, ...$ActionArgs) as parameters
#   - writes all tool output (dotnet/vpk/git) to $LogFile via 2>&1 | Add-Content
#   - throws on failure
#   - returns a string (or $null) for the dim-grey suffix shown after the time
#
# When $UseSpinner is true the action runs in a fresh runspace so the parent
# can animate the spinner. Each runspace boots from the default initial
# session state (all standard cmdlets present) which is enough for what the
# step bodies do; they intentionally don't call into helpers from this file.

function Invoke-Step {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$Label,
        [Parameter(Mandatory)] [string]$LogFile,
        [Parameter(Mandatory)] [scriptblock]$Action,
        [object[]]$ActionArgs = @()
    )

    $sw      = [System.Diagnostics.Stopwatch]::StartNew()
    $info    = $null
    $errMsg  = $null

    if ($UseSpinner) {
        $rs = [runspacefactory]::CreateRunspace()
        $rs.Open()
        $ps = [powershell]::Create()
        $ps.Runspace = $rs
        [void]$ps.AddScript($Action.ToString())
        [void]$ps.AddArgument($LogFile)
        foreach ($a in $ActionArgs) { [void]$ps.AddArgument($a) }

        $handle = $ps.BeginInvoke()
        $i = 0
        while (-not $handle.IsCompleted) {
            $frame = $SpinnerFrames[$i % $SpinnerFrames.Length]
            Write-StepLine -Mark $frame -MarkColor Yellow -Label $Label
            Start-Sleep -Milliseconds 100
            $i++
        }

        $sw.Stop()
        try {
            $result = $ps.EndInvoke($handle)
            $info   = $result | Select-Object -Last 1
        } catch {
            $errMsg = $_.Exception.Message
        }
        if (-not $errMsg -and $ps.HadErrors) {
            $errMsg = ($ps.Streams.Error | ForEach-Object { $_.ToString() }) -join "`n"
        }
        $ps.Dispose()
        $rs.Close()
        $rs.Dispose()
    } else {
        Write-StepLine -Mark '...' -MarkColor Yellow -Label $Label -Final
        try {
            $result = & $Action $LogFile @ActionArgs
            $info   = $result | Select-Object -Last 1
        } catch {
            $errMsg = $_.Exception.Message
        }
        $sw.Stop()
    }

    if ($errMsg) {
        Write-StepLine -Mark $MarkFail -MarkColor Red -Label $Label -Seconds $sw.Elapsed.TotalSeconds -Final
        throw [pscustomobject]@{
            Step    = $Label
            LogFile = $LogFile
            Message = $errMsg
        }
    }

    Write-StepLine -Mark $MarkOk -MarkColor Green -Label $Label -Seconds $sw.Elapsed.TotalSeconds -Info ([string]$info) -Final

    if (Test-Path $LogFile) {
        Add-Content $script:CombinedLog "==== $Label ===="
        Get-Content $LogFile | Add-Content $script:CombinedLog
        Add-Content $script:CombinedLog ""
    }
}

# --- pure-PowerShell helpers (run synchronously in parent) ------------------

function Get-CurrentVersion {
    param([string]$ManifestPath, [string]$PackageId)
    if (-not (Test-Path $ManifestPath)) {
        throw "Velopack manifest not found at $ManifestPath"
    }
    $manifest = Get-Content $ManifestPath | ConvertFrom-Json
    $v = $manifest.Assets |
        Where-Object { $_.PackageId -eq $PackageId } |
        ForEach-Object { [Version]$_.Version } |
        Sort-Object -Descending |
        Select-Object -First 1
    if (-not $v) { throw "No '$PackageId' entries found in $ManifestPath" }
    return $v
}

function Get-NextVersion {
    param([Version]$Current, [string]$Type)
    switch ($Type) {
        'BugFix' { "$($Current.Major).$($Current.Minor).$($Current.Build + 1)" }
        'Minor'  { "$($Current.Major).$($Current.Minor + 1).0" }
        'Major'  { "$($Current.Major + 1).0.0" }
    }
}

function Write-Header {
    param([string]$OldVersion, [string]$NewVersion, [string]$Type, [switch]$DryRun)
    Write-Host ""
    if ($DryRun) {
        Write-Host "  ZeroPlus OMS Release  " -ForegroundColor Cyan -NoNewline
        Write-Host "[DRY RUN]" -ForegroundColor Yellow
        Write-Host ("  {0} -> {1}  ({2})" -f $OldVersion, $NewVersion, $Type) -ForegroundColor White
        Write-Host "  no sync, no upload, no commit, no tag" -ForegroundColor DarkGray
    } else {
        Write-Host "  ZeroPlus OMS Release" -ForegroundColor Cyan
        Write-Host ("  {0} -> {1}  ({2})" -f $OldVersion, $NewVersion, $Type) -ForegroundColor White
    }
    Write-Host ""
}

function Write-Failure {
    param($StepError, [string]$ExtraHint)
    Write-Host ""
    Write-Host ("  {0} failed." -f $StepError.Step) -ForegroundColor Red
    Write-Host ("  {0}" -f $StepError.Message) -ForegroundColor DarkGray
    if (Test-Path $StepError.LogFile) {
        Write-Host ""
        Write-Host ("  Last 30 lines of {0}:" -f (Split-Path $StepError.LogFile -Leaf)) -ForegroundColor Yellow
        Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
        Get-Content -Tail 30 $StepError.LogFile | ForEach-Object {
            Write-Host "  $_" -ForegroundColor DarkGray
        }
        Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkGray
    }
    if ($ExtraHint) {
        Write-Host ""
        Write-Host "  $ExtraHint" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "  Full log: $script:CombinedLog" -ForegroundColor Yellow
    Write-Host ""
}

# --- main flow --------------------------------------------------------------

New-Item -ItemType Directory -Path $RunDir     -Force | Out-Null
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
'' | Set-Content $CombinedLog

$logs = [pscustomobject]@{
    preflight  = Join-Path $RunDir '01-preflight.log'
    sync       = Join-Path $RunDir '02-sync.log'
    killswitch = Join-Path $RunDir '03-build-killswitch.log'
    ui         = Join-Path $RunDir '04-build-ui.log'
    pack       = Join-Path $RunDir '05-pack.log'
    upload     = Join-Path $RunDir '06-upload.log'
    tag        = Join-Path $RunDir '07-commit-tag.log'
}
foreach ($p in $logs.psobject.Properties.Value) { '' | Set-Content $p }

Push-Location $RepoRoot
$totalSw    = [System.Diagnostics.Stopwatch]::StartNew()
$uploadDone = $false
$tagPushed  = $false

try {
    Invoke-Step -Label 'Preflight' -LogFile $logs.preflight -Action {
        param($LogFile)
        foreach ($t in 'git','dotnet','vpk') {
            if (-not (Get-Command $t -ErrorAction SilentlyContinue)) {
                throw "Required tool '$t' is not on PATH."
            }
        }
        # vpk is a .NET tool - probe its runtime so a missing runtime fails
        # in <1s instead of 175s into the build.
        $probe = & vpk -h 2>&1 | Out-String
        Add-Content $LogFile $probe
        if ($probe -match 'must install.*\.NET') {
            throw "vpk's .NET runtime is missing. Reinstall: dotnet tool uninstall -g vpk; dotnet tool install -g vpk"
        }
    }

    if (-not $DryRun) {
        Invoke-Step -Label 'Sync source control' -LogFile $logs.sync -Action {
            param($LogFile, $RepoRoot)
            Set-Location $RepoRoot
            & git fetch origin                2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw 'git fetch failed' }
            & git checkout master             2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw 'git checkout master failed' }
            & git reset --hard origin/master  2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw 'git reset --hard origin/master failed' }
            & git clean -fd                   2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw 'git clean failed' }
        } -ActionArgs @($RepoRoot)
    }

    if (-not (Test-Path $ReleaseNotesPath)) {
        throw "Release notes file not found: $ReleaseNotesPath"
    }

    $oldVersion = Get-CurrentVersion -ManifestPath (Join-Path $SharedDrivePath 'releases.win.json') -PackageId $PackageId
    $newVersion = Get-NextVersion   -Current $oldVersion -Type $ReleaseType
    Write-Header -OldVersion $oldVersion -NewVersion $newVersion -Type $ReleaseType -DryRun:$DryRun

    Invoke-Step -Label 'Build Kill Switch' -LogFile $logs.killswitch -Action {
        param($LogFile, $Csproj, $Pubxml, $RepoRoot)
        Set-Location $RepoRoot
        & dotnet publish $Csproj -p:PublishProfile=$Pubxml 2>&1 | Add-Content $LogFile
        if ($LASTEXITCODE -ne 0) { throw "Kill Switch publish failed (exit $LASTEXITCODE)" }
    } -ActionArgs @($KsCsproj, $KsPubxml, $RepoRoot)

    Invoke-Step -Label "Build OMS UI $newVersion" -LogFile $logs.ui -Action {
        param($LogFile, $Csproj, $Pubxml, $BuildOutput, $Configuration, $Version, $MainExe, $RepoRoot)
        Set-Location $RepoRoot
        & dotnet publish $Csproj `
            -c $Configuration `
            -p:Version=$Version `
            -p:AssemblyVersion=$Version `
            -p:PublishProfile=$Pubxml 2>&1 | Add-Content $LogFile
        if ($LASTEXITCODE -ne 0) { throw "OMS UI publish failed (exit $LASTEXITCODE)" }
        foreach ($exe in $MainExe, 'Resources\OMS Kill Switch.exe') {
            if (-not (Test-Path (Join-Path $BuildOutput $exe))) {
                throw "Required executable missing after publish: $exe"
            }
        }
    } -ActionArgs @($UiCsproj, $UiPubxml, $UiBuildOutput, $BuildConfiguration, $newVersion, $MainExe, $RepoRoot)

    Invoke-Step -Label 'Pack Velopack release' -LogFile $logs.pack -Action {
        param($LogFile, $Share, $StagingDir, $UiBuildOutput, $Version, $PackageId, $PackTitle, $MainExe, $Framework, $Channel, $NotesPath, $IconPath, $RepoRoot, $SkipDownload, $PackPortable, $SnapshotPath)
        Set-Location $RepoRoot

        if (-not $SkipDownload) {
            & vpk download local --path $Share --channel $Channel --outputDir $StagingDir 2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw "vpk download failed from $Share" }
        }

        # Snapshot every file that exists right now so the upload step can
        # skip anything `vpk pack` leaves untouched (i.e. the seed files we
        # just pulled from the share for delta computation).
        $snapshot = Get-ChildItem -Path $StagingDir -File | ForEach-Object {
            [pscustomobject]@{
                Name             = $_.Name
                Length           = $_.Length
                LastWriteTimeUtc = $_.LastWriteTimeUtc.Ticks
            }
        }
        ,@($snapshot) | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $SnapshotPath -Encoding UTF8

        $packArgs = @(
            'pack',
            '-u', $PackageId,
            '-v', $Version,
            '--packDir',     $UiBuildOutput,
            '--outputDir',   $StagingDir,
            '--channel',     $Channel,
            '--packTitle',   $PackTitle,
            '--mainExe',     $MainExe,
            '--framework',   $Framework,
            '--releaseNotes', $NotesPath,
            '--icon',        $IconPath,
            '--shortcuts',   'Desktop,StartMenuRoot',
            '--delta',       'BestSpeed',
            '--exclude',     '.*\.pdb|.*\.xml'
        )
        if (-not $PackPortable) {
            $packArgs += '--noPortable'
        }

        & vpk @packArgs 2>&1 | Add-Content $LogFile
        if ($LASTEXITCODE -ne 0) { throw "vpk pack failed (exit $LASTEXITCODE)" }

        $setup = Get-ChildItem $StagingDir -Filter '*-Setup.exe' | Select-Object -First 1
        if (-not $setup) { throw 'vpk pack succeeded but no *-Setup.exe was produced' }
        $sizeMb = [math]::Round($setup.Length / 1MB, 0)
        $deltaNote = if ($SkipDownload) { 'full only' } else { 'delta + full' }
        $portableNote = if ($PackPortable) { ', + portable zip' } else { '' }
        "$deltaNote, $sizeMb MB setup$portableNote"
    } -ActionArgs @($SharedDrivePath, $StagingDir, $UiBuildOutput, $newVersion, $PackageId, $PackTitle, $MainExe, $VpkFramework, $VpkChannel, $ReleaseNotesPath, $IconPath, $RepoRoot, [bool]$DryRun, [bool]$PackPortable, $PreSnapshot)

    if (-not $DryRun) {
        # Custom upload (instead of `vpk upload local`) so we can:
        #   1. Rename <PackageId>-<Version>-Setup.exe to setup-<Version>.exe
        #      on the share (vpk has no rename hook).
        #   2. Try every file even if one fails - vpk aborts on the first
        #      error, leaving subsequent files unprocessed.
        #   3. Skip files that came from the share unchanged - vpk download
        #      seeded $StagingDir with the prior manifest + last full nupkg
        #      so vpk pack could compute deltas; re-uploading those is
        #      wasted bandwidth (and worse on slow SMB links). We diff
        #      against the pre-pack snapshot and only push files that vpk
        #      pack created or modified.
        Invoke-Step -Label 'Upload to share' -LogFile $logs.upload -Action {
            param($LogFile, $Share, $StagingDir, $Version, $RepoRoot, $SnapshotPath)
            Set-Location $RepoRoot

            if (-not (Test-Path $Share)) {
                throw "Share not reachable: $Share"
            }

            # Build a Name -> {Length, LastWriteTimeUtc} lookup of the files
            # that existed before vpk pack ran. Anything still matching in
            # size and timestamp is a seed file we don't need to re-upload.
            $preExisting = @{}
            if (Test-Path -LiteralPath $SnapshotPath) {
                $raw = Get-Content -LiteralPath $SnapshotPath -Raw
                if ($raw) {
                    $entries = $raw | ConvertFrom-Json
                    foreach ($e in @($entries)) {
                        if ($e -and $e.Name) {
                            $preExisting[$e.Name] = [pscustomobject]@{
                                Length = [int64]$e.Length
                                Ticks  = [int64]$e.LastWriteTimeUtc
                            }
                        }
                    }
                }
            }

            $all = Get-ChildItem -Path $StagingDir -File
            if (-not $all) { throw "No files in $StagingDir to upload" }

            $toUpload = @()
            $skipped  = @()
            foreach ($f in $all) {
                $prior = $preExisting[$f.Name]
                if ($prior -and $prior.Length -eq $f.Length -and $prior.Ticks -eq $f.LastWriteTimeUtc.Ticks) {
                    $skipped += $f.Name
                    "SKIP  $($f.Name) (seed file, unchanged)" | Add-Content $LogFile
                } else {
                    $toUpload += $f
                }
            }

            if (-not $toUpload) {
                throw "Nothing to upload: every file in $StagingDir matched the pre-pack snapshot"
            }

            # Upload order (avoid exposing manifests before payloads land):
            #   1) .exe, .zip, .nupkg              — installers / bundles / packages
            #   2) everything else                 — uncommon stray artifacts
            #   3) .json and RELEASES*           — Velopack feed files last
            $ordered = @(
                $toUpload | Sort-Object @{ Expression = {
                        $e = $_.Extension.ToLowerInvariant()
                        if ($e -in '.exe', '.zip', '.nupkg') { 1 }
                        elseif ($e -eq '.json' -or $_.Name -like 'RELEASES*') { 3 }
                        else { 2 }
                    }; Ascending = $true },
                    @{ Expression = 'Name'; Ascending = $true }
            )

            $okCount  = 0
            $failures = @()

            foreach ($f in $ordered) {
                # Only the installer gets renamed; nupkgs and manifests must
                # keep their original names because releases.win.json
                # references the nupkgs by file name.
                $destName = if ($f.Name -like '*-Setup.exe') {
                    "setup-$Version.exe"
                } else {
                    $f.Name
                }
                $destPath = Join-Path $Share $destName

                # 3 attempts with a short backoff covers the SMB hiccups we
                # see in practice without dragging the step out on a real
                # share outage.
                $err = $null
                for ($i = 1; $i -le 3; $i++) {
                    try {
                        Copy-Item -LiteralPath $f.FullName -Destination $destPath -Force -ErrorAction Stop
                        $err = $null
                        break
                    } catch {
                        $err = $_
                        if ($i -lt 3) { Start-Sleep -Milliseconds 500 }
                    }
                }

                if ($err) {
                    $msg = "FAIL  $($f.Name) -> $destName : $($err.Exception.Message)"
                    Add-Content $LogFile $msg
                    $failures += $msg
                } else {
                    $okCount++
                    "OK    $($f.Name) -> $destName" | Add-Content $LogFile
                }
            }

            if ($failures.Count -gt 0) {
                $summary = "Upload finished with $($failures.Count)/$($ordered.Count) failure(s):`n  " +
                           ($failures -join "`n  ")
                throw $summary
            }

            $skipNote = if ($skipped.Count) { ", $($skipped.Count) seed file(s) skipped" } else { '' }
            "$okCount file(s) -> $Share$skipNote"
        } -ActionArgs @($SharedDrivePath, $StagingDir, $newVersion, $RepoRoot, $PreSnapshot)
        $uploadDone = $true

        Invoke-Step -Label "Commit + tag v$newVersion" -LogFile $logs.tag -Action {
            param($LogFile, $Version, $Type, $NotesPath, $RepoRoot, $CommitterName, $CommitterEmail)
            Set-Location $RepoRoot
            $tag = "v$Version"

            $existing = & git ls-remote --tags origin "refs/tags/$tag" 2>&1
            Add-Content $LogFile $existing
            if ($existing) {
                throw "Tag $tag already exists on origin. Delete it first: git push --delete origin $tag"
            }

            # Stamp the release commit and annotated tag with a dedicated
            # identity so they show up as "OMS Builder" in history regardless
            # of who ran the release. Scoped to this runspace's process
            # environment - the operator's global git config is untouched.
            $env:GIT_AUTHOR_NAME     = $CommitterName
            $env:GIT_AUTHOR_EMAIL    = $CommitterEmail
            $env:GIT_COMMITTER_NAME  = $CommitterName
            $env:GIT_COMMITTER_EMAIL = $CommitterEmail

            # Build the commit message: 'Release vX.Y.Z' subject line, blank line,
            # then the release notes file verbatim as the body.
            $msgFile = Join-Path $env:TEMP ("release-msg-" + [guid]::NewGuid().ToString() + ".txt")
            try {
                "Release $tag"          | Set-Content $msgFile -Encoding UTF8
                ""                      | Add-Content $msgFile -Encoding UTF8
                Get-Content $NotesPath  | Add-Content $msgFile -Encoding UTF8

                & git commit --allow-empty -F $msgFile 2>&1 | Add-Content $LogFile
                if ($LASTEXITCODE -ne 0) { throw "git commit --allow-empty failed" }
            } finally {
                Remove-Item $msgFile -Force -ErrorAction SilentlyContinue
            }

            & git push origin master 2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw "git push origin master failed" }

            & git tag -a $tag -m "Release $tag - $Type" 2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) { throw "git tag -a failed" }

            & git push origin $tag 2>&1 | Add-Content $LogFile
            if ($LASTEXITCODE -ne 0) {
                throw "git push origin $tag failed (commit landed but tag did not - retry: git push origin $tag)"
            }
            $tag
        } -ActionArgs @($newVersion, $ReleaseType, $ReleaseNotesPath, $RepoRoot, $ReleaseCommitterName, $ReleaseCommitterEmail)
        $tagPushed = $true
    }

    $totalSw.Stop()
    Write-Host ""
    if ($DryRun) {
        Write-Host ("  Dry run complete in {0}." -f (Format-Elapsed $totalSw.Elapsed)) -ForegroundColor Green
        Write-Host "  Artifacts: $StagingDir" -ForegroundColor White
        Write-Host "  Log:       $CombinedLog" -ForegroundColor DarkGray
    } else {
        Write-Host ("  Released v{0} in {1}." -f $newVersion, (Format-Elapsed $totalSw.Elapsed)) -ForegroundColor Green
        Write-Host "  Log: $CombinedLog" -ForegroundColor DarkGray
    }
    Write-Host ""
}
catch {
    $stepErr = if ($_.TargetObject -and $_.TargetObject.Step) { $_.TargetObject } else {
        [pscustomobject]@{ Step = '(setup)'; LogFile = $CombinedLog; Message = $_.Exception.Message }
    }

    $hint = $null
    if ($DryRun) {
        $hint = "Dry run aborted before completion. Partial artifacts (if any): $StagingDir"
    } elseif ($uploadDone -and -not $tagPushed) {
        $hint = "Upload succeeded but the tag did not. The release is on the share; retry just the tag:`n  git tag -a v$newVersion -m 'Release v$newVersion - $ReleaseType'`n  git push origin v$newVersion"
    } elseif (-not $uploadDone) {
        $hint = "Nothing was deployed. Staged artifacts (for manual copy if needed): $StagingDir"
    }

    Write-Failure -StepError $stepErr -ExtraHint $hint
    exit 1
}
finally {
    Pop-Location
}
