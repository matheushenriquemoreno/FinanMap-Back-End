# Runbook — homologação MCP 100% local

Este runbook sobe MongoDB, API, Prometheus e Grafana exclusivamente no Docker local. Nenhum comando publica imagem, acessa servidor remoto, altera DNS ou remove volumes.

## Pré-requisitos

- Docker Desktop com `docker compose`.
- .NET SDK 9.
- PowerShell 7.
- Portas livres: API `17270`, Prometheus `19090`, Grafana `13000`.

## 1. Preparar configuração e certificados

```powershell
Set-Location 'D:\FinamMap\FinanMap-Back-End\Modulos\GerenciamentoMensal'

Copy-Item '.env.mcp-staging.example' '.env.mcp-staging.local'
New-Item -ItemType Directory -Path '.mcp-local-secrets'

dotnet dev-certs https `
  -ep '.mcp-local-secrets\mcp-signing.pfx' `
  -p 'SUBSTITUA_SENHA_CERTIFICADO'

Copy-Item `
  '.mcp-local-secrets\mcp-signing.pfx' `
  '.mcp-local-secrets\mcp-encryption.pfx'
```

Edite `.env.mcp-staging.local`, substitua todo `CHANGE_ME` e use:

- `MCP_STAGING_IMAGE=finanmap-mcp-staging:<SHA_LOCAL>`;
- `SOURCE_REVISION=<SHA_LOCAL>`;
- `MCP_PUBLIC_BASE_URL=http://127.0.0.1:17270`;
- `MCP_ALLOWED_ORIGINS=http://localhost:9000`;
- `FRONT_END_URLS=http://localhost:9000`;
- `MCP_CERTIFICATES_DIR=D:\FinamMap\FinanMap-Back-End\Modulos\GerenciamentoMensal\.mcp-local-secrets`;
- a mesma senha local nos dois certificados;
- segredos aleatórios distintos para Mongo, JWT, cursor, prévia e Grafana.

O arquivo `.env.mcp-staging.local` e os certificados `*.pfx` são ignorados pelo Git.

## 2. Validar e subir

```powershell
.\scripts\mcp-local-config.ps1 `
  -EnvFile '.env.mcp-staging.local'

.\scripts\mcp-local-up.ps1 `
  -EnvFile '.env.mcp-staging.local'
```

O `up` constrói a imagem por SHA e aguarda todos os healthchecks.

## 3. Validar saúde e observabilidade

```powershell
.\scripts\mcp-local-health.ps1 `
  -EnvFile '.env.mcp-staging.local'
```

Interfaces locais:

- API: `http://127.0.0.1:17270/healthcheck`
- métricas: `http://127.0.0.1:17270/metrics`
- Prometheus: `http://127.0.0.1:19090`
- Grafana: `http://127.0.0.1:13000`

No Prometheus, o target `finanmap-mcp-api` deve estar `UP`. No Grafana, a pasta `FinanMap` deve conter o dashboard provisionado `FinanMap MCP - Operação mínima`.

## 4. Executar as dez jornadas e latência

```powershell
.\scripts\mcp-local-journeys.ps1
.\scripts\mcp-local-latency.ps1
```

As jornadas J01–J10 usam as suítes de integração/contrato rastreadas em `PHASE-7-TRACEABILITY.md`. O gate de latência valida P95 de leitura até 3 s e confirmação até 5 s.

## 5. Smoke consolidado

```powershell
.\scripts\mcp-staging-smoke.ps1 `
  -EnvFile '.env.mcp-staging.local'
```

Não use `-Cleanup` quando quiser inspecionar Prometheus/Grafana após o teste.

## 6. Ensaio de rollback

Primeiro execute somente o dry-run:

```powershell
.\scripts\mcp-staging-rollback.ps1 `
  -EnvFile '.env.mcp-staging.local' `
  -PreviousImage 'finanmap-mcp-staging:<SHA_ANTERIOR>'
```

Revise se o plano contém, nesta ordem, writes desabilitados, endpoint desabilitado e imagem anterior. Somente para ensaio local autorizado:

```powershell
.\scripts\mcp-staging-rollback.ps1 `
  -EnvFile '.env.mcp-staging.local' `
  -PreviousImage 'finanmap-mcp-staging:<SHA_ANTERIOR>' `
  -Execute `
  -Confirm
```

O rollback não apaga Mongo, journals, auditoria, Prometheus ou Grafana.

## 7. Encerrar preservando evidências

```powershell
.\scripts\mcp-local-down.ps1 `
  -EnvFile '.env.mcp-staging.local'
```

Nunca use `down -v`, `docker volume rm` ou `docker system prune` neste fluxo. Para uma nova execução, os volumes locais existentes serão reutilizados.

## Diagnóstico

```powershell
docker compose `
  --profile observability `
  --env-file '.env.mcp-staging.local' `
  -f 'docker-compose.mcp-staging.yaml' `
  ps

docker compose `
  --profile observability `
  --env-file '.env.mcp-staging.local' `
  -f 'docker-compose.mcp-staging.yaml' `
  logs --tail 200 webapi prometheus grafana
```

Falhas de escrita com resultado incerto não devem ser repetidas automaticamente. Consulte journal/status e preserve os correlation IDs antes de qualquer nova tentativa.
