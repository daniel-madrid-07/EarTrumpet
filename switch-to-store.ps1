# Goes back to the Microsoft Store EarTrumpet: stops this fork, removes its autostart entry,
# re-enables the Store copy's startup task and starts it. Undo with install-local.ps1 -NoBuild.
$ErrorActionPreference = 'Stop'
Get-Process EarTrumpet -ErrorAction SilentlyContinue | Where-Object { $_.Path -notlike '*\WindowsApps\*' } | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'EarTrumpet' -ErrorAction SilentlyContinue
$storeTask = 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\40459File-New-Project.EarTrumpet_1sdd7yawvg6ne\EarTrumpet'
if (Test-Path $storeTask) { Set-ItemProperty $storeTask -Name State -Value 2 -Type DWord }
Start-Process 'shell:AppsFolder\40459File-New-Project.EarTrumpet_1sdd7yawvg6ne!EarTrumpet'
Write-Host 'Store EarTrumpet restored and started.'
