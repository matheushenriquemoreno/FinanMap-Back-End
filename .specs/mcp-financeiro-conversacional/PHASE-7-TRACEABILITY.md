# Fase 7 — rastreabilidade e aceite local

## Resultado honesto

O critério autorizado para esta fase é uma homologação 100% local e isolada. O candidato é validado no Docker local com MongoDB, API, Prometheus e Grafana; nenhuma publicação externa é necessária para o aceite local. A publicação em **homologação externa não executada** continua fora do escopo: nenhum resultado local é apresentado como evidência de produção.

## Itens da fase

| Item | Evidência |
| --- | --- |
| MCPF-P7-01 | `WebApi/Dockerfile` com bases por digest, revisão OCI e healthcheck; `docker-compose.mcp-staging.yaml` separado, Mongo por digest, segredos obrigatórios externos, certificados somente leitura, health de API/Mongo e volume persistente. |
| MCPF-P7-03 | Harness local completo: Compose com Mongo/API/Prometheus/Grafana e scripts config/up/health/smoke/down. Publicação externa não executada e não exigida pelo critério autorizado. |
| MCPF-P7-04 | Dez jornadas J01–J10 abaixo, exercitadas por testes automatizados de contrato, integração e E2E. |
| MCPF-P7-05 | Suíte completa, build Release, importação de 1.000 itens, latência, autorização, auditoria, reconciliação e conformidade. |
| MCPF-P7-06 | Exporter `/metrics`, Prometheus com scrape/regras e Grafana com datasource/dashboard provisionados, alinhados aos instrumentos reais de `McpTelemetry`; correlação limitada a IDs/tags seguros. |
| MCPF-P7-07 | `scripts/mcp-staging-rollback.ps1`: primeiro desabilita writes e endpoint, depois restaura imagem anterior, sem apagar volume, journal ou auditoria. Ensaio local em dry-run. |
| MCPF-P7-08 | `PHASE-7-PILOT-CHECKLIST.md`, com suporte, limitações, go/no-go e coleta de evidências. |

## Dez jornadas representativas

| Jornada | Escopo | Evidência automatizada |
| --- | --- | --- |
| J01 | Discovery, OAuth 2.1/PKCE, conexão e revogação | `McpOAuthHostContractTests`, `McpAuthenticatedInspectorSmokeTests`, `McpConnectionServiceTests` |
| J02 | Categorias e consultas sem confirmação | `McpCategoriesToolServiceTests`, `McpToolContractTests` |
| J03 | Transações com filtros, paginação e isolamento | `McpFinancialReadPhase2Tests`, `McpFinancialReadSourceIntegrationTests` |
| J04 | Resumo, comparação e reconciliação financeira | `McpFinancialReadPhase2Tests`, `McpLatencyGatePhase6Tests` |
| J05 | Criação por prévia e confirmação única | `McpWriteServicePhase3Tests`, `McpMongoWritePhase3IntegrationTests` |
| J06 | Alteração, idempotência e replay seguro | `McpWriteEnginePhase3Tests`, `McpOperationReconcilerPhase3Tests` |
| J07 | Exclusão, impacto, confirmação e auditoria preservada | `McpApplicationMutationServicePhase3Tests`, `McpWriteDomainGatewayPhase4Tests` |
| J08 | Importação de 1.000 itens, parcial e duplicidades | `McpImportServicePhase5Tests`, `McpImportPersistencePhase5Tests` |
| J09 | Correção/reenvio, status e histórico | `McpImportPersistencePhase5Tests`, `McpMongoAuditIntegrationTests`, testes frontend de histórico |
| J10 | Falhas, revogação, cross-account e resultado Unknown | `McpRequestSecurityMiddlewareTests`, `McpHealthCheckPhase6Tests`, `McpTelemetryPhase6Tests`, `McpAuthenticatedInspectorSmokeTests` |

## Matriz de 125 requisitos

| Faixa | Tema | Evidência principal |
| --- | --- | --- |
| MCP-01–MCP-16 | conexão, autenticação, privacidade | contratos OAuth/host, conexão, segurança e Inspector |
| MCP-17–MCP-33 | consultas e análises | leitura financeira, paginação, fontes Mongo e latência |
| MCP-34–MCP-49 | prévia, confirmação e idempotência | write engine/service, integração Mongo e reconciliador |
| MCP-50–MCP-71 | CRUD dos cinco domínios | gateways Phase3/Phase4 e mutation service |
| MCP-72–MCP-97 | importação estruturada | contratos, serviço, persistência, lote 1.000 e privacidade |
| MCP-98–MCP-114 | auditoria e histórico | integração de audit, cursor, filtros e isolamento |
| MCP-115–MCP-125 | falhas e previsibilidade | DTOs, fault/lease/Unknown, health, telemetria e conformance |

Inventário verificável: MCP-01 MCP-02 MCP-03 MCP-04 MCP-05 MCP-06 MCP-07 MCP-08 MCP-09 MCP-10 MCP-11 MCP-12 MCP-13 MCP-14 MCP-15 MCP-16 MCP-17 MCP-18 MCP-19 MCP-20 MCP-21 MCP-22 MCP-23 MCP-24 MCP-25 MCP-26 MCP-27 MCP-28 MCP-29 MCP-30 MCP-31 MCP-32 MCP-33 MCP-34 MCP-35 MCP-36 MCP-37 MCP-38 MCP-39 MCP-40 MCP-41 MCP-42 MCP-43 MCP-44 MCP-45 MCP-46 MCP-47 MCP-48 MCP-49 MCP-50 MCP-51 MCP-52 MCP-53 MCP-54 MCP-55 MCP-56 MCP-57 MCP-58 MCP-59 MCP-60 MCP-61 MCP-62 MCP-63 MCP-64 MCP-65 MCP-66 MCP-67 MCP-68 MCP-69 MCP-70 MCP-71 MCP-72 MCP-73 MCP-74 MCP-75 MCP-76 MCP-77 MCP-78 MCP-79 MCP-80 MCP-81 MCP-82 MCP-83 MCP-84 MCP-85 MCP-86 MCP-87 MCP-88 MCP-89 MCP-90 MCP-91 MCP-92 MCP-93 MCP-94 MCP-95 MCP-96 MCP-97 MCP-98 MCP-99 MCP-100 MCP-101 MCP-102 MCP-103 MCP-104 MCP-105 MCP-106 MCP-107 MCP-108 MCP-109 MCP-110 MCP-111 MCP-112 MCP-113 MCP-114 MCP-115 MCP-116 MCP-117 MCP-118 MCP-119 MCP-120 MCP-121 MCP-122 MCP-123 MCP-124 MCP-125.

## Limites da evidência

- O harness local não comprova DNS, certificado TLS público, roteamento, CSP do frontend implantado, integração do provedor de métricas nem permissões do ambiente externo.
- Nenhum piloto humano foi executado; a meta de conclusão por participantes depende do piloto autorizado.
- A retenção de auditoria e o texto jurídico de processamento por agentes continuam decisões de Produto/Privacidade.
- Os alertas são configuração versionada; o carregamento em Prometheus/Grafana externo precisa ser comprovado no ambiente.

## Execução local integrada no Docker Desktop

Com um `.env.mcp-staging.local` derivado do exemplo e sem `CHANGE_ME`, execute a partir de `Modulos/GerenciamentoMensal`:

```powershell
docker compose --env-file .env.mcp-staging.local -f docker-compose.mcp-staging.yaml up -d --build --wait
Invoke-RestMethod https://localhost:17271/healthcheck
Start-Process http://localhost:9071/
```

O serviço `frontend` só inicia após `webapi` saudável. `HOMOLOG_URL_API` usa
`https://localhost:17271/api/` porque as chamadas são feitas pelo navegador host;
não use `webapi` nessa variável. O healthcheck do frontend é `http://localhost:9071/`.
Para encerrar sem destruir journals/auditoria: `docker compose --env-file .env.mcp-staging.local -f docker-compose.mcp-staging.yaml down` (não usar `down -v`).

## Critério de aceite local

- Compose válido com os quatro serviços e imagens externas fixadas por digest.
- API expõe `/healthcheck` e `/metrics`; Prometheus reporta o target da API como `UP`.
- Grafana inicia com datasource e dashboard provisionados, sem cadastro anônimo.
- Scripts PowerShell cobrem configuração, subida, saúde, dez jornadas, latência, rollback e encerramento sem remoção de volume.
- `RUNBOOK-LOCAL-HOMOLOG.md` é a fonte operacional reproduzível.
- Deploy externo, DNS e TLS público não pertencem ao aceite local autorizado.

## Gates executados em 2026-07-28

- RED inicial: 4/4 testes de artefatos falharam pelos arquivos operacionais ausentes.
- GREEN focado: 4/4 `McpStagingArtifactsPhase7Tests` aprovados.
- `docker compose config --quiet`: aprovado com configuração efêmera, sem segredo persistido.
- Build containerizado: imagem local `finanmap-mcp-staging:f05d77f`, OCI revision `f05d77f`, image ID `sha256:e2dc0308f18f623a26225a471528b401b095c0c1a6cb3f0ff3031856a95565f6`.
- Smoke containerizado: Mongo `Healthy`, API `Healthy`, `/healthcheck` retornou `Healthy`; contêineres/redes removidos sem `-v`, volume preservado.
- Rollback dry-run: aprovado, com flags desabilitadas antes da troca de imagem e sem remoção de dados.
- Backend Release: 215/215 testes aprovados; warnings preexistentes, zero falhas.
- `dotnet format --verify-no-changes` nos arquivos C# da fase e `git diff --check`: aprovados.
- Ampliação local de observabilidade — RED: teste de contrato falhou pela ausência de exporter, Prometheus, Grafana, scripts e runbook.
- Ampliação local de observabilidade — GREEN: 5/5 `McpStagingArtifactsPhase7Tests` aprovados.
- Stack local: Mongo, API, Prometheus e Grafana ficaram `Healthy`; target da API `UP`.
- Prometheus: 10 regras versionadas carregadas.
- Grafana: datasource `finanmap-prometheus` e dashboard `FinanMap MCP - Operação mínima` encontrados pela API.
- Jornadas: J01–J10 aprovadas pelo script `mcp-local-journeys.ps1`.
- Latência: `LATENCY_OK read_p95<=3s write_p95<=5s`.
- Encerramento: `LOCAL_DOWN_OK volumes=preserved`; `docker compose ps --all` sem contêineres.
