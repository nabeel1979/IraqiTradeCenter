# Builds Release publish and optionally zips or copies to IIS folder (run where paths exist).
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OutDir = "",
    [string]$ZipPath = "",
    [string]$CopyTo = ""
)

$ErrorActionPreference = "Stop"
$proj = Join-Path $RepoRoot "src\Host\IraqiTradeCenterCompany.API\IraqiTradeCenterCompany.API.csproj"
if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

if (-not $OutDir) { $OutDir = Join-Path $RepoRoot "publish" }
Write-Host "Publishing to: $OutDir"
dotnet publish $proj -c Release -o $OutDir --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$count = (Get-ChildItem $OutDir -File -Recurse | Measure-Object).Count
Write-Host "Published file count: $count"

if ($ZipPath) {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $ZipPath -Force
    $sizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "ZIP created: $ZipPath ($sizeMb MB)"
    Write-Host "On the server: copy the ZIP and scripts\Install-FromZip-OnServer.ps1, then run:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File Install-FromZip-OnServer.ps1 -ZipPath `"<full path to zip>`""
}

if ($CopyTo) {
    if (-not (Test-Path $CopyTo)) { New-Item -ItemType Directory -Path $CopyTo -Force | Out-Null }
    Copy-Item -Path (Join-Path $OutDir "*") -Destination $CopyTo -Recurse -Force
    Write-Host "Copied to: $CopyTo"
}
