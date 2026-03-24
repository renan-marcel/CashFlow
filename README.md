# CashFlow

[![codecov](https://codecov.io/github/renan-marcel/CashFlow/graph/badge.svg?token=0L23ITS0FV)](https://codecov.io/github/renan-marcel/CashFlow) [![SonarQube Cloud](https://sonarcloud.io/images/project_badges/sonarcloud-light.svg)](https://sonarcloud.io/summary/new_code?id=renan-marcel_CashFlow) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Aspire](https://img.shields.io/badge/Aspire-13.x-6F42C1?style=flat-square) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white) ![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)

API de fluxo de caixa com processamento assíncrono de consolidação diária.

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Pré-requisitos](#pré-requisitos)
- [Como Executar](#como-executar)
- [Endpoints Principais](#endpoints-principais)
- [Testes](#testes)
- [Documentação](#documentação)
- [Configuração](#configuração)
- [Decisões Técnicas](#decisões-técnicas)
- [Limitações e Roadmap](#limitações-e-roadmap)

---

## Visão Geral

**CashFlow** é uma solução de gerenciamento de fluxo de caixa que resolve o desafio técnico de registrar lançamentos (débito/crédito) com consolidação automática de saldos diários. A arquitetura garante:

- ✅ **Disponibilidade**: Serviço de lançamentos continua operando mesmo se o consolidado falhar
- ✅ **Escalabilidade**: Suporta 50 req/s no consolidado com máximo 5% de perda de requisições
- ✅ **Confiabilidade**: Padrão Outbox garante exactly-once semantics
- ✅ **Resiliência**: Desacoplamento total entre lançamento (síncrono) e consolidação (assíncrono)

### Fluxo principal

```
1. Cliente registra lançamento (crédito/débito) via POST /api/v1/ledger-entries
   ↓
2. API valida, persiste com idempotência e cria mensagem no Outbox
   ↓
3. OutboxDispatcher publica evento no RabbitMQ (a cada 3 segundos)
   ↓
4. Worker consome evento, consolida saldo diário e marca como processado
   ↓
5. Cliente consulta saldo consolidado via GET /api/v1/daily-balances
```

---

## Arquitetura

A solução segue uma **arquitetura em camadas modular com Domain-Driven Design (DDD)**, combinando padrões de resiliência:

```
Apresentação (Controllers)
    ↓
Aplicação (Application Services, Command/Query)
    ↓
Domínio (Entidades, Validadores, Regras de Negócio)
    ↓
Infraestrutura (Persistência, Mensageria, Outbox)
```

### Componentes principais

| Componente | Tecnologia | Responsabilidade |
|-----------|-----------|------------------|
| **CashFlow.Api** | ASP.NET Core 10 | REST HTTP, versionamento, validação |
| **CashFlow.Worker** | .NET Hosted Service | Consumidor RabbitMQ, consolidação |
| **CashFlow.Domain** | C# 12 | Entidades, regras de negócio, validadores |
| **CashFlow.Application** | C# 12 | Casos de uso, orquestração |
| **CashFlow.Infrastructure** | EF Core 10, Npgsql, RabbitMQ | Persistência, mensageria, Outbox pattern |
| **CashFlow.Migrations** | EF Core | Versionamento de banco de dados |
| **CashFlow.AppHost** | .NET Aspire 13 | Orquestração local (Docker Compose) |
| **Tests** | xUnit 2.9, FluentAssertions | Testes unitários e integração |

Para mais detalhes, consulte [docs/architecture.md](docs/architecture.md). Os diagramas C4 interativos estão disponíveis em [docs/c4-diagrams.drawio](docs/c4-diagrams.drawio) (importar no [draw.io](https://draw.io)).

---

## Pré-requisitos

### Obrigatório

- **[.NET SDK 10.0.x](https://dotnet.microsoft.com/download)**
- **[Docker Desktop](https://www.docker.com/products/docker-desktop)** (ou Docker Engine + Docker Compose)
- **[Git](https://git-scm.com)**

### Opcional

- `dotnet-reportgenerator-globaltool` (para relatórios de cobertura local)

---

## Como Executar

### ✨ Opção 1: Com AppHost (Aspire) — RECOMENDADO

Esta é a forma mais simples. O AppHost orquestra automaticamente PostgreSQL, RabbitMQ, Migrations, API e Worker.

```bash
# 1. Clone o repositório
git clone https://github.com/renan-marcel/CashFlow.git
cd CashFlow

# 2. Restaure as dependências
dotnet restore

# 3. Execute o AppHost
dotnet run --project src/AppHost/CashFlow.AppHost/CashFlow.AppHost.AppHost

# 4. Acesse a aplicação
# Dashboard Aspire: http://localhost:15297
# Swagger API:    http://localhost:5189/swagger
# RabbitMQ Mgmt:  http://localhost:15672 (user/password: guest/guest)
# pgAdmin:        http://localhost:5050
```

Quando rodando, você verá uma URL do dashboard do Aspire no terminal. Todos os serviços serão provisionados automaticamente.

### 📦 Opção 2: Execução Manual (sem AppHost)

Para quem não quer usar Aspire.

#### Passo 1: Subir dependências (Docker)

```bash
# PostgreSQL
docker run -d --name cashflow-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=cashflow \
  -p 5432:5432 \
  postgres:16

# RabbitMQ Management Plugin
docker run -d --name cashflow-rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management
```

#### Passo 2: Executar Migrations

```bash
dotnet run --project src/CashFlow.Migrations
```

Saída esperada: `✅ Migrations aplicadas com sucesso!` ou `✅ Banco de dados está atualizado.`

#### Passo 3: Executar serviços (em terminais separados)

**Terminal 1 - Worker:**
```bash
dotnet run --project src/CashFlow.Worker
```

Saída esperada: `Consumidor de consolidação ativo na fila cashflow.ledger.consolidation`

**Terminal 2 - API:**
```bash
dotnet run --project src/CashFlow.Api
```

Saída esperada: `info: Microsoft.Hosting.Lifetime[14] - Now listening on: http://localhost:5189`

Agora a API está pronta em `http://localhost:5189/swagger`.

---

## Endpoints Principais

### 1. Criar Lançamento

**Endpoint:**  
`POST /api/v1/ledger-entries`

**Request:**
```bash
curl -X POST http://localhost:5189/api/v1/ledger-entries \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "11111111-1111-1111-1111-111111111111",
    "type": "credit",
    "amount": 150.75,
    "occurredAt": "2026-03-24T10:00:00Z",
    "description": "Venda PDV #001",
    "idempotencyKey": "txn-20260324-0001"
  }'
```

**Response (201 Created):**
```json
{
  "ledgerEntryId": "550e8400-e29b-41d4-a716-446655440000",
  "merchantId": "11111111-1111-1111-1111-111111111111",
  "type": "credit",
  "amount": 150.75,
  "occurredAtUtc": "2026-03-24T10:00:00Z",
  "description": "Venda PDV #001",
  "idempotencyKey": "txn-20260324-0001",
  "isDuplicate": false
}
```

**Validações:**
- `merchantId`: Não pode ser GUID vazio
- `type`: Deve ser "credit" ou "debit" (case-insensitive)
- `amount`: Deve ser > 0
- `occurredAt`: Não pode ser no futuro
- `idempotencyKey`: Obrigatório, máx 256 caracteres
- Duplicidade: Se mesmo `merchantId` + `idempotencyKey`, retorna entrada anterior com `isDuplicate: true`

### 2. Consultar Saldo Diário

**Endpoint:**  
`GET /api/v1/daily-balances?merchantId={guid}&date={yyyy-MM-dd}`

**Request:**
```bash
curl "http://localhost:5189/api/v1/daily-balances?merchantId=11111111-1111-1111-1111-111111111111&date=2026-03-24" \
  -H "Accept: application/json"
```

**Response (200 OK):**
```json
{
  "merchantId": "11111111-1111-1111-1111-111111111111",
  "date": "2026-03-24",
  "balance": 150.75,
  "updatedAtUtc": "2026-03-24T10:05:00Z"
}
```

**Error (404 Not Found):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Saldo não encontrado",
  "status": 404,
  "detail": "Não há saldo consolidado para os filtros informados."
}
```

### 3. Swagger UI

Acesse a documentação interativa em:  
`http://localhost:5189/swagger`

---

## Testes

### Executar todos os testes

```bash
dotnet test
```

### Testes com cobertura

#### Windows (PowerShell)

```powershell
./scripts/run-coverage.ps1
```

#### Linux/macOS (Bash)

```bash
bash ./scripts/run-coverage.sh
```

**Relatório gerado em:**  
`coverage/html/index.html`

Abrir no navegador para visualizar detalhes de cobertura por arquivo.

### Tipos de testes

| Tipo | Localização | Escopo | Exemplo |
|------|-------------|--------|---------|
| **Unitários** | `tests/CashFlow.UnitTests/` | Domínio, validadores | Validação de entidades |
| **Integração** | `tests/CashFlow.IntegrationTests/` | Application Services, Outbox | Persistência + Outbox, Consolidação |
| **Contrato** | Não implementado | API contracts | Swagger validação |
| **E2E** | Não implementado | Fluxo completo | Criar → Consolidar → Query |

**Cobertura alvo:** 70% (mínimo), 80% (target)

---

## Documentação

### Arquivos principais

| Arquivo | Conteúdo |
|---------|----------|
| **[docs/architecture.md](docs/architecture.md)** | Visão detalhada de arquitetura, componentes, fluxos, padrões |
| **[docs/c4-diagrams.drawio](docs/c4-diagrams.drawio)** | Diagramas C4 (Contexto, Containers, Componentes) — importar no draw.io |
| **[docs/adrs.md](docs/adrs.md)** | Architecture Decision Records (decisões técnicas e trade-offs) |
| **[docs/gaps-and-roadmap.md](docs/gaps-and-roadmap.md)** | Lacunas atuais, riscos, melhorias futuras |
| **[README.md](README.md)** | Este arquivo |

---

## Configuração

### Variáveis de ambiente (Execução manual)

Se não usar AppHost, configure as connection strings via variáveis:

```bash
# PostgreSQL
export ConnectionStrings__cashflowdb="Host=localhost;Database=cashflow;Username=postgres;Password=postgres;Port=5432"

# RabbitMQ (automático se AppHost)
export RABBITMQ_HOST=localhost
export RABBITMQ_PORT=5672
```

### appsettings.json

Os arquivos de configuração estão predefinidos:

- `src/CashFlow.Api/appsettings.json`
- `src/CashFlow.Migrations/appsettings.json`
- `src/CashFlow.Worker/appsettings.json`
- `src/CashFlow.Infrastructure/appsettings.json`

Para ambientes, crie:
- `appsettings.Development.json`
- `appsettings.Production.json`

---

## Decisões Técnicas

### 1. Padrão Outbox para garantir Exactly-Once Semantics

**Decisão:** Usar Outbox Pattern em vez de publicar direto no RabbitMQ.

**Motivo:**
- Garante atomicidade: LedgerEntry + OutboxMessage em uma transação
- Não há perda de mensagens em caso de falha entre DB e MQ
- Permite retry automático sem duplicação

**Alternativa descartada:** Saga pattern (mais complexo, não necessário aqui)

### 2. Consolidação a cada 3 segundos

**Decisão:** OutboxDispatcher processa a cada 3 segundos.

**Motivo:**
- Latência aceitável para o caso de uso
- Reduz carga no RabbitMQ
- Processa em lotes (até 50 mensagens)

**Alternativa:** Real-time com confirmação imediata (mais complexo, não foi justificado)

### 3. PostgreSQL + EF Core

**Decisão:** Usar PostgreSQL com Entity Framework Core.

**Motivo:**
- Cross-platform (Linux, macOS, Windows)
- Open source, free
- EF Core oferece migrations automáticas
- Native support em Aspire
- ACID guarantees

**Alternativa:** SQL Server (enterprise, licenca), Dapper (menor abstração)

### 4. Idempotência via `IdempotencyKey` + `ProcessedIntegrationEvent`

**Decisão:** Double-write para garantir idempotência.

**Motivo:**
- Lançamentos: `IdempotencyKey` único (UNIQUE index)
- Eventos: Tabela `ProcessedIntegrationEvent` com `EventId` único
- Protege contra retries acidentais

**Alternativa:** Cache distribuído (adiciona overhead)

### 5. RabbitMQ para mensageria

**Decisão:** RabbitMQ em vez de Kafka.

**Motivo:**
- Prototipagem rápida com Aspire
- Latência baixa (< 1s)
- Modelos de entrega (Direct exchange, routing keys)
- Management plugin incluído

**Alternativa:** Kafka (high-throughput, maior overhead)

---

## Limitações e Roadmap

### Limitações atuais

| Limitação | Impacto | Prioridade |
|-----------|--------|-----------|
| **Sem autenticação/autorização** | Endpoints públicos, segurança baixa | 🔴 ALTA |
| **Sem rate limiting** | Vulnerável a DDoS | 🔴 ALTA |
| **Sem circuit breaker RabbitMQ** | Falhas prolongadas podem acumular retry | 🟡 MÉDIA |
| **Outbox a cada 3s** | Latência até 3 segundos | 🟡 MÉDIA (aceitável) |
| **Sem cache de saldos** | Leitura sempre hit DB | 🟡 MÉDIA |
| **Health checks limitados** | Visibilidade baixa em produção | 🟡 MÉDIA |
| **Sem auditoria de transações** | Rastreamento baixo | 🟢 BAIXA |

### Roadmap (curto, médio, longo prazo)

**Curto prazo (1-2 semanas):**
- Adicionar OAuth2/JWT para autenticação
- Implementar rate limiting via Polly
- Adicionar health checks para DB e RabbitMQ
- Expandir testes E2E

**Médio prazo (1-2 meses):**
- Implementar circuit breaker para RabbitMQ
- Adicionar Redis para cache de saldos
- Melhorar observabilidade (Prometheus, Grafana)
- Adicionar auditoria de transações

**Longo prazo (2+ meses):**
- Multi-tenant support
- Suporte a múltiplas moedas
- Dashboard de analytics
- Migração para microsserviços (se necessário)

---

## Estrutura do repositório

```
CashFlow/
├── docs/
│   ├── architecture.md          # Documentação detalhada
│   ├── adrs.md                  # Architecture Decision Records
│   └── gaps-and-roadmap.md      # Lacunas e roadmap
├── src/
│   ├── AppHost/                 # Aspire orchestration
│   ├── CashFlow.Api/            # API HTTP
│   ├── CashFlow.Application/    # Application services
│   ├── CashFlow.Domain/         # Domain logic
│   ├── CashFlow.Infrastructure/ # Persistence, Messaging
│   ├── CashFlow.Migrations/     # EF Core migrations
│   └── CashFlow.Worker/         # Background service
├── tests/
│   ├── CashFlow.UnitTests/      # Unit tests
│   └── CashFlow.IntegrationTests/ # Integration tests
├── scripts/
│   ├── run-coverage.ps1         # Coverage Windows
│   └── run-coverage.sh          # Coverage Linux/Mac
├── .github/workflows/           # CI/CD (GitHub Actions)
├── CashFlow.slnx                # Solução
├── Directory.Packages.props     # Central package versions
├── codecov.yml                  # Codecov config
├── README.md                    # Este arquivo
└── gitignore.txt
```

---

## CI/CD

A solução está configurada com GitHub Actions:

- **[build-and-test.yml](.github/workflows/build-and-test.yml)**: Build, testes, cobertura, upload Codecov
- **[code-quality.yml](.github/workflows/code-quality.yml)**: SonarCloud analysis
- **[performance.yml](.github/workflows/performance.yml)**: Testes de performance, benchmarks

### Status

[![Build and Test](https://github.com/renan-marcel/CashFlow/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/renan-marcel/CashFlow/actions)
[![Code Quality](https://github.com/renan-marcel/CashFlow/actions/workflows/code-quality.yml/badge.svg?branch=main)](https://github.com/renan-marcel/CashFlow/actions)

---

## Contribuindo

1. Fork o repositório
2. Crie uma branch para sua feature: `git checkout -b feature/my-feature`
3. Commit suas mudanças: `git commit -m "Add my feature"`
4. Push para a branch: `git push origin feature/my-feature`
5. Abra um Pull Request

---

## Licença

[MIT License](LICENSE)

---

## Contato

**Autor:** Renan Marcel  
**Repositório:** [github.com/renan-marcel/CashFlow](https://github.com/renan-marcel/CashFlow)  
**Issues:** [GitHub Issues](https://github.com/renan-marcel/CashFlow/issues)

---

**Última atualização:** Março 2026  
**Versão da solução:** 1.0.0
