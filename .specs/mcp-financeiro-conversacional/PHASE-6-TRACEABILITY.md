# Phase 6 — Rastreabilidade executável

| Item | Evidência executável |
| --- | --- |
| MCPF-P6-01 / MCP-112 | `McpRequestSecurityMiddlewareTests`, `McpSdkTransportContractTests`: Origin/CORS, HTTPS/media type, rate limit, revogação e isolamento de titular. |
| MCPF-P6-02 / MCP-113 | `McpSdkTransportContractTests` e contratos das 31 ferramentas: schemas fechados, classificação read/write e `import_confirm` aditivo. |
| MCPF-P6-03 / MCP-115..121 | `McpTelemetryPhase6Tests`, `McpHealthCheckPhase6Tests`: correlation ID, métricas limitadas, logs redigidos, alertas e health de journal/lease/reconciliador. |
| MCPF-P6-04 / MCP-115,118,119 | `McpLatencyGatePhase6Tests`, índices Mongo e testes de consulta por intervalo/categorias em lote; P95 read ≤3s e confirmação ≤5s. |
| MCPF-P6-05 | `McpAuditHistory.spec.ts`, `McpAuditHistory.vue`: filtros, estados vazios/loading/erro, retry e detalhes seguros. |
| MCPF-P6-06 | `IntegracaoIaConfig.spec.ts`, `McpAuditHistory.spec.ts`: revogação/retry, aria, foco, teclado, alvos de 44px, responsividade, dark mode e aviso externo. |
| MCPF-P6-07 / MCP-112..113 | `McpAuthenticatedInspectorSmokeTests`: SDK .NET oficial e Inspector CLI autenticados como clientes independentes; descoberta e tool call. |
| MCPF-P6-08 / MCP-115..125 | Suítes de segurança, fault injection, lease/Unknown, reconciliação, latência e revogação HTTP E2E; 211/211 backend e 89/89 frontend. |
| MCPF-P6-09 | Revisão independente PASS e commits separados da fase. |

## Requisitos transversais

`MCP-112`, `MCP-113` e `MCP-115`–`MCP-125` possuem evidência nas linhas acima; nenhum gap conhecido permanece no escopo da Fase 6.
