# CashFlow

[![codecov](https://codecov.io/github/renan-marcel/CashFlow/graph/badge.svg?token=0L23ITS0FV)](https://codecov.io/github/renan-marcel/CashFlow) [![SonarQube Cloud](https://sonarcloud.io/images/project_badges/sonarcloud-light.svg)](https://sonarcloud.io/summary/new_code?id=renan-marcel_CashFlow) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Aspire](https://img.shields.io/badge/Aspire-13.x-6F42C1?style=flat-square) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white) ![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)

API de fluxo de caixa com processamento assíncrono de consolidação diária.

## Visão geral

A solução é composta por:

- **CashFlow.Api**: API HTTP para criação de lançamentos e consulta de saldo diário.
- **CashFlow.Worker**: consumidor RabbitMQ que processa eventos e consolida saldos.
- **CashFlow.Migrations**: executa migrations do Entity Framework no PostgreSQL.
- **CashFlow.AppHost.AppHost**: orquestração local com .NET Aspire (PostgreSQL + RabbitMQ + serviços).
- **Tests**: testes unitários e de integração.

Fluxo principal:

1. A API recebe um lançamento (`credit`/`debit`).
2. O lançamento é persistido e um evento é enviado via outbox.
3. Worker consome o evento no RabbitMQ.
4. Worker atualiza o saldo diário consolidado no banco.

## Pré-requisitos

- .NET SDK **10.0.x**
- Docker Desktop (ou Docker Engine)
- Git

Opcional (para cobertura local):

- `dotnet-reportgenerator-globaltool`

## Como executar (recomendado) — AppHost (Aspire)

> Esta é a forma mais simples: o AppHost sobe PostgreSQL, RabbitMQ, migrations, API e Worker.

1. Restaurar pacotes:

```bash
dotnet restore
```

2. Executar AppHost:

```bash
dotnet run --project src/AppHost/CashFlow.AppHost/CashFlow.AppHost.AppHost
```

3. Acompanhar os recursos no dashboard do Aspire (link exibido no console).

### Recursos criados pelo AppHost

- RabbitMQ com management plugin.
- PostgreSQL com banco `cashflowdb`.
- Execução de migrations antes da API/Worker.

## Como executar manualmente (sem AppHost)

Use este modo apenas se não quiser usar o Aspire.

### 1) Subir dependências (Docker)

PostgreSQL:

```bash
docker run -d --name cashflow-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=cashflow \
  -p 5432:5432 postgres:16
```

RabbitMQ:

```bash
docker run -d --name cashflow-rabbitmq \
  -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 2) Executar migrations

```bash
dotnet run --project src/CashFlow.Migrations
```

### 3) Executar Worker e API (terminais separados)

> Se necessário, informe as connection strings via variáveis de ambiente:
> - `ConnectionStrings__cashflowdb`
> - `ConnectionStrings__rabbitmq`

Worker:

```bash
dotnet run --project src/CashFlow.Worker
```

API:

```bash
dotnet run --project src/CashFlow.Api
```

Por padrão (launch profile `http`), a API sobe em `http://localhost:5189`.

## Endpoints principais

A API usa versionamento em rota (`v1`).

### Criar lançamento

`POST /api/v1/ledger-entries`

Exemplo:

```bash
curl -X POST http://localhost:5189/api/v1/ledger-entries \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "11111111-1111-1111-1111-111111111111",
    "type": "credit",
    "amount": 150.75,
    "occurredAt": "2026-03-24T10:00:00Z",
    "description": "Venda PDV",
    "idempotencyKey": "txn-20260324-0001"
  }'
```

### Consultar saldo diário

`GET /api/v1/daily-balances?merchantId={guid}&date=YYYY-MM-DD`

Exemplo:

```bash
curl "http://localhost:5189/api/v1/daily-balances?merchantId=11111111-1111-1111-1111-111111111111&date=2026-03-24"
```

### Swagger (ambiente Development)

- `http://localhost:5189/swagger`

## Testes

Executar todos os testes:

```bash
dotnet test
```

## Cobertura de testes

### Windows (PowerShell)

```powershell
./scripts/run-coverage.ps1
```

### Linux/macOS (bash)

```bash
bash ./scripts/run-coverage.sh
```

O relatório HTML é gerado em:

- `coverage/html/index.html`

## SonarCloud (CI)

O pipeline está preparado para SonarCloud. Configure no repositório GitHub:

- Secret `SONAR_TOKEN`

## Estrutura do repositório

```text
src/
  AppHost/
  CashFlow.Api/
  CashFlow.Application/
  CashFlow.Domain/
  CashFlow.Infrastructure/
  CashFlow.Migrations/
  CashFlow.Worker/
tests/
  CashFlow.UnitTests/
  CashFlow.IntegrationTests/
```
