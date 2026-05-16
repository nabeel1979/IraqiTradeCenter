# Run ON THE IIS server (PowerShell as Administrator). Unpacks the publish ZIP into the site folder.
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [string]$SiteRoot = "D:\iraqitradecenter\IraqiTradeCenter_Company"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "ZIP not found: $ZipPath"
}

$localCfg = Join-Path $SiteRoot "appsettings.Local.json"
$backup = $null
if (Test-Path -LiteralPath $localCfg) {
    $backup = Join-Path $env:TEMP "appsettings.Local.json.itc-backup"
    Copy-Item -LiteralPath $localCfg -Destination $backup -Force
    Write-Host "Backed up appsettings.Local.json"
}

$temp = Join-Path $env:TEMP ("itc-unpack-" + [Guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $temp -Force
    New-Item -ItemType Directory -Path $SiteRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $temp "*") -Destination $SiteRoot -Recurse -Force
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

if ($backup) {
    Copy-Item -LiteralPath $backup -Destination $localCfg -Force
    Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
    Write-Host "Restored appsettings.Local.json"
}

$dll = Join-Path $SiteRoot "IraqiTradeCenterCompany.API.dll"
$verify = Join-Path $SiteRoot "Deploy-Verify.txt"
$wc = Join-Path $SiteRoot "web.config"
Write-Host ""
Write-Host "Files in site root:"
Get-ChildItem -LiteralPath $SiteRoot | Select-Object -First 25 Name
Write-Host ""
if ((Test-Path -LiteralPath $dll) -and (Test-Path -LiteralPath $wc)) {
    Write-Host "OK: DLL and web.config present."
} else {
    throw "Deploy failed: missing DLL or web.config under $SiteRoot"
}
if (Test-Path -LiteralPath $verify) {
    Write-Host "OK: Deploy-Verify.txt present (IIS path is correct)."
} else {
    Write-Host "Warning: Deploy-Verify.txt missing (old ZIP?). Check DLL anyway."
}
