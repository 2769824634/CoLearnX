param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5144,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$clientRoot = Join-Path $projectRoot 'colearnx.client'
$serverRoot = Join-Path $projectRoot 'CoLearnX.Server'
$previewRoot = Join-Path $projectRoot 'artifacts/local-preview'
$runtimeRoot = Join-Path $previewRoot 'server'
$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim()

if (-not $NoBuild) {
    Push-Location $clientRoot
    try {
        if (-not (Test-Path -LiteralPath 'node_modules')) {
            & npm.cmd ci
            if ($LASTEXITCODE -ne 0) { throw 'Client dependency installation failed.' }
        }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
    } finally { Pop-Location }
    & dotnet build (Join-Path $serverRoot 'CoLearnX.Server.csproj') -p:SkipSpaPublish=true -p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
}

$buildRoot = Join-Path $serverRoot 'bin/Debug/net10.0'
if (-not (Test-Path -LiteralPath (Join-Path $buildRoot 'CoLearnX.Server.dll')) -or
    -not (Test-Path -LiteralPath (Join-Path $clientRoot 'dist/index.html'))) {
    throw 'Build output is missing. Run again without -NoBuild.'
}
New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
Copy-Item -Path (Join-Path $buildRoot '*') -Destination $runtimeRoot -Recurse -Force

# Testing uses a separate SQLite database and captures email locally.
# Command-line settings take precedence over developer configuration.
$serverArguments = @(
    (Join-Path $runtimeRoot 'CoLearnX.Server.dll'),
    '--environment=Testing',
    "--urls=http://127.0.0.1:$Port",
    "--contentRoot=$serverRoot",
    "--webroot=$(Join-Path $clientRoot 'dist')",
    "--ConnectionStrings:Default=Data Source=$(Join-Path $previewRoot 'preview.db')",
    '--Storage:ConnectionString=',
    "--Storage:RootPath=$(Join-Path $previewRoot 'uploads')",
    "--PasswordReset:ClientBaseUrl=http://127.0.0.1:$Port",
    '--PasswordReset:DeliveryMode=Auto'
)
Write-Host "$version local preview: http://127.0.0.1:$Port"
Write-Host 'Keep this window open. Press Ctrl+C to stop.'
& dotnet @serverArguments
exit $LASTEXITCODE
