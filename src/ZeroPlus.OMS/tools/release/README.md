# Release tooling

Single-file PowerShell script that ships a new ZeroPlus OMS release end to
end: bump version, build, package with Velopack (with delta), upload to the
network share, commit and tag.

## Quick start

From the repo root:

```powershell
# bug-fix release (3.166.0 -> 3.166.1)
.\tools\release\Release.ps1

# minor release  (3.166.0 -> 3.167.0)
.\tools\release\Release.ps1 -ReleaseType Minor

# major release  (3.166.0 -> 4.0.0)
.\tools\release\Release.ps1 -ReleaseType Major

# local-only test build (no git changes, no share, no tag)
.\tools\release\Release.ps1 -DryRun
```

That's it. The script reads the current shipped version from the share, bumps
it per `-ReleaseType`, builds, uploads, then commits an empty `Release vX.Y.Z`
on `master` and pushes the matching annotated tag.

## What the script does, in order

1. **Preflight** – verifies `git`, `dotnet`, `vpk` are on `PATH` and that
   `vpk` can actually start (catches a missing .NET runtime in <1s instead
   of 175s into the build).
2. **Sync source control** – `git fetch` then hard-reset `master` to
   `origin/master`, then `git clean -fd`. Local changes are discarded.
3. **Calculate version** – reads
   `\\zpapp01.corp.zeroplusderivatives.com\oms\releases.win.json`, finds the
   latest `ZeroPlus-OMS-UI` version, increments per `-ReleaseType`.
4. **Build Kill Switch** – `dotnet publish` of `ZeroPlus.OMS.KillSwitch`
   into `ZeroPlus.Oms.Ui\Resources\` (output is git-ignored).
5. **Build OMS UI** – `dotnet publish` of `ZeroPlus.Oms.Ui` with
   `-p:Version`/`-p:AssemblyVersion` set to the new version.
6. **Pack Velopack release** – `vpk download local` to fetch existing
   releases for delta computation, then `vpk pack` with `--delta BestSpeed`.
   By default `--noPortable` is passed so Velopack does **not** emit the
   `*-Portable.zip` bundle; pass `-PackPortable` when you want that zip too.
7. **Upload to share** – plain `Copy-Item` loop into
   `\\zpapp01.corp.zeroplusderivatives.com\oms`. Upload order is
   deterministic: `.exe`, `.zip`, and `.nupkg` files first, any other loose
   files next, then `.json` and `RELEASES*` feed files last so a partial
   failure never publishes a manifest that references packages still
   uploading. Each file is retried up to 3 times with a 500ms backoff to absorb
   transient SMB hiccups, and a single file's failure no longer aborts
   the rest of the upload (we collect failures and surface them all at
   the end). The installer is renamed
   `ZeroPlus-OMS-UI-X.Y.Z-Setup.exe` -> `setup-X.Y.Z.exe` on the share;
   nupkgs and manifests keep their original names because
   `releases.win.json` references the nupkgs by file name.
8. **Commit + tag** – `git commit --allow-empty` on `master` with subject
   `Release vX.Y.Z` and the contents of `ReleaseNotes.md` as the body
   (no file diff – the notes themselves live in the commit message), push
   `master`, create annotated tag `vX.Y.Z`, push tag.

Order is deliberate: the share upload (the user-facing, hard-to-undo step)
runs before the commit + tag. If the tag step fails after upload, the release
is on the share and you just need to retry the tag manually – the script
prints the exact command.

## Parameters

| Name                  | Default                                  | Notes |
|-----------------------|------------------------------------------|-------|
| `-ReleaseType`        | `BugFix`                                 | `BugFix` (patch), `Minor`, or `Major`. |
| `-BuildConfiguration` | `Release`                                | `Debug` is for local smoke tests only. Production releases must be `Release`. |
| `-ReleaseNotesPath`   | `<repo>\ReleaseNotes.md`                 | Markdown file passed to `vpk pack --releaseNotes`. Its contents also become the body of the release commit message. Update this **before** running the script. |
| `-DryRun`             | _off_                                    | Local-only test mode. Computes the next version from the share manifest exactly like a real run, but skips git sync, the vpk download for delta computation, the share upload, and the commit + tag step. Artifacts land in `%TEMP%\ZeroPlusOMSRelease\<runId>\velopack-out\`. See [Dry run mode](#dry-run-mode). |
| `-PackPortable`       | _off_                                    | Velopack normally also builds a portable zip (`*-Portable.zip`). Without this switch the script passes `--noPortable` so only Setup.exe / nupkg / manifest artifacts are produced (smaller, faster pack). |

Few CLI switches by design. Things like share path, package id, repo name,
and target framework are constants at the top of the script – edit the file
if any of them ever changes.

## Prerequisites

| Tool          | Why                                            | Install                                                                 |
|---------------|------------------------------------------------|-------------------------------------------------------------------------|
| Git 2.40+     | Source control                                 | `winget install Git.Git`  /  `choco install git -y`                     |
| .NET SDK 8.0+ | `dotnet publish`                               | `winget install Microsoft.DotNet.SDK.8`  /  `choco install dotnet-8.0-sdk -y` |
| .NET Runtime  | Required by `vpk` itself                       | Comes with the SDK; if missing, `winget install Microsoft.DotNet.Runtime.8` |
| Velopack CLI  | Packaging                                      | `dotnet tool install -g vpk`                                            |

Authentication and access:

- **Network share** `\\zpapp01.corp.zeroplusderivatives.com\oms` must be
  writable from the machine running the script (Product Owner only).
- **Git push to `origin/master`** must be allowed for the operator's account.
- **VPN** must be connected for both the share and the GitHub Enterprise host.

## Output

### On success

```
  ZeroPlus OMS Release
  3.166.0 -> 3.167.0  (Minor)

  [check] Preflight                            0.2s
  [check] Sync source control                  3.4s
  [check] Build Kill Switch                    2.1s
  [check] Build OMS UI 3.167.0                 82.6s
  [check] Pack Velopack release                38.5s   delta + full, 234 MB setup
  [check] Upload to share                      12.8s   \\zpapp01.corp.zeroplusderivatives.com\oms
  [check] Commit + tag v3.167.0                0.9s

  Released v3.167.0 in 2m 21s.
  Log: C:\Users\<you>\AppData\Local\Temp\ZeroPlusOMSRelease\20260511-120300\release.log
```

`[check]` is a green Unicode check mark; the spinner that ticks while a
step runs is yellow.

### On failure

The failed step is marked with a red cross and the last 30 lines of that
step's log are printed inline:

```
  [x]     Build OMS UI 3.167.0                 12.4s

  Build OMS UI 3.167.0 failed.
  OMS UI publish failed (exit 1)

  Last 30 lines of 04-build-ui.log:
  ----------------------------------------------------------------
  ...tail of dotnet output...
  ----------------------------------------------------------------

  Full log: C:\Users\<you>\AppData\Local\Temp\ZeroPlusOMSRelease\20260511-120300\release.log
```

### Logs

A per-run directory under `%TEMP%\ZeroPlusOMSRelease\<timestamp>\` contains:

- `01-preflight.log` … `07-commit-tag.log` – per-step tool output.
- `release.log` – all per-step logs concatenated, with section headers.

Logs are never written inside the repo and are not auto-cleaned (so they
stick around for inspection after a release).

## Dry run mode

`-DryRun` is for verifying the build/pack pipeline (or the script itself)
without touching anything that ships. Useful when:

- Iterating on the script.
- Smoke-testing a build off your own working tree.
- Producing an installable `Setup.exe` stamped with the real next version,
  without actually publishing it.

What it skips:

| Real step                  | Dry run behaviour                              |
|----------------------------|------------------------------------------------|
| Sync source control        | Skipped entirely. Whatever is checked out is what gets built (uncommitted changes included). |
| `vpk download local`       | Skipped. The pack runs without a previous-version source, so only a full nupkg is produced (no delta). |
| Upload to share            | Skipped. Artifacts stay in `%TEMP%\ZeroPlusOMSRelease\<runId>\velopack-out\`. |
| Commit + tag + push        | Skipped entirely. No git history is created, nothing is pushed to `origin`. |

What it still does:

- Preflight tool checks (`git`, `dotnet`, `vpk` on PATH; `vpk` runtime probe).
- Reads `releases.win.json` from the share and bumps the version per
  `-ReleaseType`, so the build is stamped with the same `vX.Y.Z` a real
  release would produce. Share access (and therefore VPN) is still
  required for this step.
- `dotnet publish` of both Kill Switch and OMS UI.
- `vpk pack` (full nupkg + Setup.exe by default; add `-PackPortable` for
  `*-Portable.zip` too), with the bumped version stamped in.

Sample output:

```
  ZeroPlus OMS Release  [DRY RUN]
  3.166.0 -> 3.167.0  (Minor)
  no sync, no upload, no commit, no tag

  [check] Preflight                            0.2s
  [check] Build Kill Switch                    2.1s
  [check] Build OMS UI 3.167.0                 82.4s
  [check] Pack Velopack release                21.1s   full only, 234 MB setup

  Dry run complete in 1m 46s.
  Artifacts: C:\Users\<you>\AppData\Local\Temp\ZeroPlusOMSRelease\20260511-130045\velopack-out
  Log:       C:\Users\<you>\AppData\Local\Temp\ZeroPlusOMSRelease\20260511-130045\release.log
```

The output directory is the same `%TEMP%\ZeroPlusOMSRelease\<runId>\` used
by real runs, so you can install the produced `*-Setup.exe` directly to
verify it works end to end. Because the version matches the next real
release, do **not** install a dry-run `Setup.exe` on a machine that
expects the official build of the same version - uninstall before
shipping the real release, or run with a `-ReleaseType` you don't intend
to use next (e.g., `-ReleaseType Major` for a dev sanity check).

## Troubleshooting

### `Required tool 'vpk' is not on PATH`
`dotnet tool install -g vpk` and reopen the terminal so the
`%USERPROFILE%\.dotnet\tools` PATH entry takes effect.

### `vpk's .NET runtime is missing`
The `vpk` tool was installed against a .NET runtime that's no longer
present. Cleanest fix:

```powershell
dotnet tool uninstall -g vpk
dotnet tool install   -g vpk
```

### `Tag vX.Y.Z already exists on origin`
A previous run tagged this version. Either bump again with `-ReleaseType`
or delete the tag first:

```powershell
git push --delete origin vX.Y.Z
git tag -d vX.Y.Z
```

### Upload succeeded but the tag step failed
The release **is on the share** – do not re-run the script (it would try
to bump the version again). Just retry the tag:

```powershell
git tag -a vX.Y.Z -m "Release vX.Y.Z - <Type>"
git push origin vX.Y.Z
```

(The script's failure output prints these exact commands when this case
hits.)

### `Velopack manifest not found at \\zpapp01...\releases.win.json`
VPN is not connected, or the share is unreachable. Verify with
`Test-Path '\\zpapp01.corp.zeroplusderivatives.com\oms'` and check VPN.

### Unicode boxes / question marks in the status UI
Classic `conhost.exe` on an old Windows build. Either use Windows Terminal
/ PowerShell 7, or run the script with `-Verbose` so you can also see step
output via the per-step logs in `%TEMP%`. The script already calls
`[Console]::OutputEncoding = UTF8` at startup, which fixes most cases.

### Spinner doesn't animate
You're either in the PowerShell ISE host (no real console) or have
`$env:CI` set. The script falls back to a non-animated `... <Label>`
line per step – functionally identical, just less pretty.

## Verification after release

```powershell
# Tag is on origin
git ls-remote --tags origin vX.Y.Z

# Setup is on the share (renamed from ZeroPlus-OMS-UI-X.Y.Z-Setup.exe)
Test-Path '\\zpapp01.corp.zeroplusderivatives.com\oms\setup-X.Y.Z.exe'

# Manifest reflects the new version
(Get-Content '\\zpapp01.corp.zeroplusderivatives.com\oms\releases.win.json' | ConvertFrom-Json).Assets |
    Where-Object { $_.PackageId -eq 'ZeroPlus-OMS-UI' -and $_.Version -eq 'X.Y.Z' }
```

## Modifying the script

The script is intentionally one file. Edit it directly when something needs
to change:

- **Constants block** at the top covers the share path, package id, target
  framework, project paths, and icon. These rarely change.
- **Step bodies** are inline scriptblocks inside the main flow rather than
  named helpers. This keeps each step self-contained for the runspace
  worker that powers the spinner – don't refactor them into shared helpers
  without checking that the helpers are reachable from a fresh runspace.

After edits, sanity-check with:

```powershell
# Parser smoke check (no execution)
$tokens=$errors=$null
[System.Management.Automation.Language.Parser]::ParseFile(
    'tools\release\Release.ps1', [ref]$tokens, [ref]$errors) | Out-Null
if ($errors) { $errors } else { 'Parse OK' }
```
