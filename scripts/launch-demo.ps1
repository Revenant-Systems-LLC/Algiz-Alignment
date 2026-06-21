# Algiz CFO Demo — one command, zero API keys
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "  ALGIZ GOVERNANCE CONSOLE" -ForegroundColor "#C0A96A"
Write-Host "  Demo mode — no LLM API keys required" -ForegroundColor DarkGray
Write-Host ""

$apiJob = Start-Job -ScriptBlock {
    param($root)
    Set-Location $root
    $env:SAIGE_DEMO_MODE = "true"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
    dotnet run --project src/SageRage.Api --no-build 2>&1
} -ArgumentList $Root

Write-Host "Building API..." -ForegroundColor Cyan
dotnet build "$Root/src/SageRage.Api/SageRage.Api.csproj" -v q
if ($LASTEXITCODE -ne 0) { Stop-Job $apiJob; Remove-Job $apiJob; exit 1 }

# Restart with fresh build
Stop-Job $apiJob -ErrorAction SilentlyContinue
Remove-Job $apiJob -ErrorAction SilentlyContinue

$apiJob = Start-Job -ScriptBlock {
    param($root)
    Set-Location $root
    $env:SAIGE_DEMO_MODE = "true"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
    dotnet run --project src/SageRage.Api --no-build
} -ArgumentList $Root

Write-Host "Waiting for API on :5000..." -ForegroundColor Cyan
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        Invoke-RestMethod -Uri "http://localhost:5000/api/dashboard" -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) {
    Write-Host "API failed to start. Check job output:" -ForegroundColor Red
    Receive-Job $apiJob
    exit 1
}
Write-Host "API ready." -ForegroundColor Green

Write-Host "Starting web console on http://localhost:3000 ..." -ForegroundColor Cyan
Write-Host ""
Write-Host "  Dashboard:  http://localhost:3000" -ForegroundColor White
Write-Host "  Trace log:  http://localhost:3000/trace" -ForegroundColor White
Write-Host "  Swagger:    http://localhost:5000/swagger" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Click 'Run CFO Scenario' for the liability demo." -ForegroundColor Yellow
Write-Host "  Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

Push-Location "$Root/src/SageRage.Web"
try {
    npm run dev
} finally {
    Pop-Location
    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -ErrorAction SilentlyContinue
}
