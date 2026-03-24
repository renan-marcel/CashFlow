#!/bin/bash

# Script para rodar testes com cobertura localmente
# Simula o que o GitHub Actions faz

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=====================================${NC}"
echo -e "${BLUE}  CashFlow - Local Test & Coverage${NC}"
echo -e "${BLUE}=====================================${NC}"
echo ""

# Verificar se .NET está instalado
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ .NET not found. Please install .NET 10${NC}"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✅ .NET Version: $DOTNET_VERSION${NC}"
echo ""

# 1. Limpar
echo -e "${YELLOW}🧹 Cleaning previous builds...${NC}"
dotnet clean --configuration Release > /dev/null 2>&1 || true
rm -rf coverage/ 2>/dev/null || true
echo ""

# 2. Restore
echo -e "${YELLOW}📦 Restoring dependencies...${NC}"
dotnet restore
echo ""

# 3. Build
echo -e "${YELLOW}🏗️  Building project...${NC}"
dotnet build --configuration Release --no-restore
echo ""

# 4. Testes com cobertura
echo -e "${YELLOW}🧪 Running tests with coverage...${NC}"
dotnet test \
  --configuration Release \
  --no-build \
  --no-restore \
  --logger "console;verbosity=minimal" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo ""

# 5. Verificar se foi gerado cobertura
if [ ! -f "coverage/coverage.cobertura.xml" ]; then
    # Tentar encontrar em outro lugar
    COVERAGE_FILE=$(find coverage -name "*.cobertura.xml" -type f | head -1)
    if [ -z "$COVERAGE_FILE" ]; then
        echo -e "${RED}⚠️  Coverage file not found${NC}"
    else
        echo -e "${GREEN}✅ Coverage file found: $COVERAGE_FILE${NC}"
    fi
else
    echo -e "${GREEN}✅ Coverage file generated successfully${NC}"
fi

echo ""

# 6. Instalar ReportGenerator se não estiver
if ! command -v reportgenerator &> /dev/null; then
    echo -e "${YELLOW}📥 Installing ReportGenerator...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool
fi

# 7. Gerar relatório HTML
echo -e "${YELLOW}📊 Generating HTML coverage report...${NC}"
COVERAGE_PATTERN="./coverage/**/coverage.cobertura.xml"

reportgenerator \
  -reports:"$COVERAGE_PATTERN" \
  -targetdir:"./coverage/html" \
  -reporttypes:"Html;JsonSummary" \
  -classfilters:"-*Tests*" 2>/dev/null || true

if [ -d "./coverage/html" ]; then
    echo -e "${GREEN}✅ HTML report generated in ./coverage/html${NC}"
else
    echo -e "${YELLOW}⚠️  Could not generate HTML report${NC}"
fi

echo ""
echo -e "${BLUE}=====================================${NC}"
echo -e "${BLUE}  Coverage Report Generated${NC}"
echo -e "${BLUE}=====================================${NC}"
echo ""

# 8. Tentar abrir relatório
if [ -f "./coverage/html/index.html" ]; then
    echo -e "${GREEN}📄 HTML Report:${NC} ./coverage/html/index.html"
    echo ""
    
    # Detectar sistema operacional e abrir navegador
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        if command -v xdg-open &> /dev/null; then
            echo -e "${YELLOW}🌐 Opening in browser...${NC}"
            xdg-open "./coverage/html/index.html" 2>/dev/null &
        else
            echo -e "${YELLOW}📋 To view the report, open: ./coverage/html/index.html${NC}"
        fi
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        echo -e "${YELLOW}🌐 Opening in browser...${NC}"
        open "./coverage/html/index.html" 2>/dev/null &
    elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
        echo -e "${YELLOW}🌐 Opening in browser...${NC}"
        start "./coverage/html/index.html" 2>/dev/null &
    else
        echo -e "${YELLOW}📋 To view the report, open: ./coverage/html/index.html${NC}"
    fi
else
    echo -e "${RED}❌ Report file not found${NC}"
fi

echo ""

# 9. Exibir resumo JSON se existir
if [ -f "./coverage/html/Summary.json" ]; then
    echo -e "${BLUE}=====================================${NC}"
    echo -e "${BLUE}  Coverage Summary${NC}"
    echo -e "${BLUE}=====================================${NC}"
    echo ""
    cat "./coverage/html/Summary.json" | head -50
    echo ""
fi

# 10. Exibir estatísticas
echo -e "${BLUE}=====================================${NC}"
echo -e "${BLUE}  File Statistics${NC}"
echo -e "${BLUE}=====================================${NC}"
echo ""

if [ -d "./coverage" ]; then
    echo "Coverage directory contents:"
    du -sh ./coverage/* 2>/dev/null | awk '{print "  " $0}'
fi

echo ""
echo -e "${GREEN}✅ Done!${NC}"
echo ""
echo "📊 Next steps:"
echo "  1. Open ./coverage/html/index.html to view detailed report"
echo "  2. Check coverage percentages against thresholds"
echo "  3. Review areas with low coverage"
echo ""
