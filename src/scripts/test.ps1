#!/usr/bin/env pwsh
# Run tests with coverage and generate report

$ErrorActionPreference = "Stop"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Check if reportgenerator is installed
$reportGen = dotnet tool list -g | Select-String "reportgenerator"
if (-not $reportGen) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

# Generate HTML report
reportgenerator -reports:"./coverage/**/coverage.cobertura.xml" -targetdir:"./coverage/report" -reporttypes:Html

# Open report
$reportPath = "./coverage/report/index.html"
if (Test-Path $reportPath) {
    Write-Host "Opening coverage report..." -ForegroundColor Green
    if ($IsWindows -or $env:OS -match "Windows") {
        Start-Process $reportPath
    } elseif ($IsMacOS) {
        open $reportPath
    } else {
        xdg-open $reportPath
    }
} else {
    Write-Host "Report not found at $reportPath" -ForegroundColor Red
}
