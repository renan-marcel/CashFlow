# Script para rodar testes com cobertura localmente (Windows)
# Simula o que o GitHub Actions faz

param(
    [switch]$SkipBuild = $false,
    [switch]$OpenReport = $true,
    [string]$Configuration = "Release"
)

Write-Host ""
Write-Host "=====================================" -ForegroundColor Blue
Write-Host "  CashFlow - Local Test & Coverage" -ForegroundColor Blue
Write-Host "=====================================" -ForegroundColor Blue
Write-Host ""

# Verificar se .NET está instalado
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET not found. Please install .NET 10" -ForegroundColor Red
    exit 1
}

Write-Host "✅ .NET Version: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# 1. Limpar
Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean --configuration $Configuration 2>$null | Out-Null
Remove-Item -Path "./coverage" -Recurse -Force 2>$null | Out-Null
Write-Host ""

# 2. Restore
Write-Host "📦 Restoring dependencies..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Restore failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 3. Build (se não for skip)
if (-not $SkipBuild) {
    Write-Host "🏗️  Building project..." -ForegroundColor Yellow
    dotnet build --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# 4. Testes com cobertura
Write-Host "🧪 Running tests with coverage..." -ForegroundColor Yellow
dotnet test `
  --configuration $Configuration `
  --no-restore `
  --no-build `
  --logger "console;verbosity=minimal" `
  --collect:"XPlat Code Coverage" `
  --results-directory ./coverage `
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  Some tests failed (see above for details)" -ForegroundColor Yellow
}

Write-Host ""

# 5. Verificar se foi gerado cobertura
$coverageFile = Get-Item -Path "coverage/**/coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -eq $coverageFile) {
    Write-Host "⚠️  Coverage file not found" -ForegroundColor Yellow
} else {
    Write-Host "✅ Coverage file found: $($coverageFile.FullName)" -ForegroundColor Green
}

Write-Host ""

# 6. Instalar ReportGenerator se não estiver
$reportGeneratorInstalled = dotnet tool list -g | Select-String "reportgenerator"
if ($null -eq $reportGeneratorInstalled) {
    Write-Host "📥 Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# 7. Gerar relatório HTML
Write-Host "📊 Generating HTML coverage report..." -ForegroundColor Yellow
$coveragePattern = "coverage/**/coverage.cobertura.xml"

reportgenerator `
  -reports:"$coveragePattern" `
  -targetdir:"./coverage/html" `
  -reporttypes:"Html;JsonSummary" `
  -classfilters:"-*Tests*" 2>$null

if (Test-Path "./coverage/html") {
    Write-Host "✅ HTML report generated in ./coverage/html" -ForegroundColor Green
} else {
    Write-Host "⚠️  Could not generate HTML report" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Blue
Write-Host "  Coverage Report Generated" -ForegroundColor Blue
Write-Host "=====================================" -ForegroundColor Blue
Write-Host ""

# 8. Abrir relatório se solicitado
if ($OpenReport -and (Test-Path "./coverage/html/index.html")) {
    Write-Host "📄 HTML Report: ./coverage/html/index.html" -ForegroundColor Green
    Write-Host ""
    Write-Host "🌐 Opening in browser..." -ForegroundColor Yellow
    Start-Process "./coverage/html/index.html"
} else {
    Write-Host "📄 HTML Report: ./coverage/html/index.html" -ForegroundColor Green
    Write-Host "    (Use -OpenReport flag to open automatically)" -ForegroundColor Gray
}

Write-Host ""

# 9. Exibir resumo JSON se existir
if (Test-Path "./coverage/html/Summary.json") {
    Write-Host "=====================================" -ForegroundColor Blue
    Write-Host "  Coverage Summary" -ForegroundColor Blue
    Write-Host "=====================================" -ForegroundColor Blue
    Write-Host ""
    
    $summaryJson = Get-Content "./coverage/html/Summary.json" -TotalCount 50
    Write-Host $summaryJson
    Write-Host ""
}

# 10. Exibir estatísticas
Write-Host "=====================================" -ForegroundColor Blue
Write-Host "  File Statistics" -ForegroundColor Blue
Write-Host "=====================================" -ForegroundColor Blue
Write-Host ""

if (Test-Path "./coverage") {
    Write-Host "Coverage directory contents:"
    Get-ChildItem -Path "./coverage" -Recurse | 
        Measure-Object -Property Length -Sum | 
        ForEach-Object { "  Total size: {0:N2} MB" -f ($_.Sum / 1MB) }
}

Write-Host ""
Write-Host "✅ Done!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Next steps:" -ForegroundColor Cyan
Write-Host "  1. View report: ./coverage/html/index.html"
Write-Host "  2. Check coverage percentages against thresholds"
Write-Host "  3. Review areas with low coverage"
Write-Host ""

# Exibir treshold recomendado
Write-Host "📋 Coverage Thresholds:" -ForegroundColor Cyan
Write-Host "  Lines:    70% (minimum) / 80% (target)" -ForegroundColor Gray
Write-Host "  Branches: 65% (minimum) / 75% (target)" -ForegroundColor Gray
Write-Host "  Methods:  75% (minimum) / 85% (target)" -ForegroundColor Gray
Write-Host ""
