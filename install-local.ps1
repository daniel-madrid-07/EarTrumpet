# Builds this fork and installs it as the user's volume mixer:
#   %LOCALAPPDATA%\Programs\EarTrumpet, started at logon from HKCU\...\Run.
# The Store EarTrumpet is left installed; see switch-to-store.ps1 to go back to it.
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\EarTrumpet'
$exe = Join-Path $installDir 'EarTrumpet.exe'

if (-not $NoBuild) {
    & (Join-Path $root 'build-release.ps1')
    # prebuild.ps1 stamps the version into the packaging manifest; keep the tree clean.
    git -C $root checkout -- EarTrumpet.Package/Package.appxmanifest 2>$null
}

# Stop the Store copy (it stays installed) and any previous install, then copy the build.
& (Join-Path $root 'stop-eartrumpet.ps1')
New-Item -ItemType Directory -Force $installDir | Out-Null
robocopy (Join-Path $root 'Build\Release') $installDir /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "copy failed ($LASTEXITCODE)" }

# Autostart this build; keep the Store copy's startup task off so only one runs.
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'EarTrumpet' -Value "`"$exe`""
$storeTask = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\40459File-New-Project.EarTrumpet_1sdd7yawvg6ne\EarTrumpet'
if (Test-Path $storeTask) { Set-ItemProperty $storeTask -Name State -Value 1 -Type DWord }

Start-Process $exe
Write-Host "Installed and started: $exe"
