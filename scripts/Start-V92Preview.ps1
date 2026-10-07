param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5192,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$versionRoot = Split-Path -Parent $PSScriptRoot
$clientRoot = Join-Path $versionRoot 'colearnx.client'
$serverProject = Join-Path $versionRoot 'CoLearnX.Server/CoLearnX.Server.csproj'
$previewRoot = Join-Path $versionRoot 'artifacts/local-preview-v9.2'
$runtimeRoot = Join-Path $previewRoot 'server'
New-Item -ItemType Directory -Force -Path $previewRoot | Out-Null

if (-not $NoBuild) {
    Push-Location $clientRoot
    try {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
    } finally { Pop-Location }
    & dotnet build $serverProject -p:SkipSpaPublish=true -p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
}

# Testing captures account mail locally and does not load developer user secrets.
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:ASPNETCORE_URLS = "https://localhost:$Port"
$env:ConnectionStrings__Default = "Data Source=$(Join-Path $previewRoot 'preview.db')"
$env:Storage__RootPath = Join-Path $previewRoot 'uploads'
$env:PasswordReset__ClientBaseUrl = "https://localhost:$Port"
$env:PasswordReset__DeliveryMode = 'Auto'

# Run a copied build so the preview does not lock the build output used by tests.
New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
Copy-Item -Path (Join-Path $versionRoot 'CoLearnX.Server/bin/Debug/net10.0/*') -Destination $runtimeRoot -Recurse -Force
Write-Host "20261005_v9.2 preview: https://localhost:$Port"
& dotnet (Join-Path $runtimeRoot 'CoLearnX.Server.dll') `
    --contentRoot (Join-Path $versionRoot 'CoLearnX.Server') --webroot (Join-Path $clientRoot 'dist')
exit $LASTEXITCODE
