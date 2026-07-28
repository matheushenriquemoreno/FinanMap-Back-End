# Fase 7 — checklist de piloto e suporte

## Go/no-go técnico local

- [x] Suite backend completa aprovada (215/215, Release, 2026-07-28).
- [x] Suite frontend completa aprovada (89/89, lint, `vue-tsc`, build e `test:phase7`).
- [x] Build Release backend e build imutável frontend aprovados localmente.
- [x] `docker compose config --quiet` aprovado com segredos efêmeros.
- [x] Imagem backend construída com `SOURCE_REVISION` igual ao SHA candidato local `f05d77f`.
- [x] Health runtime local de Mongo/API saudável; journal/reconciliador cobertos pelos testes de readiness da Fase 6.
- [x] J01–J10 aprovadas pela suíte automatizada e anexadas à rastreabilidade.
- [x] Gate de 1.000 itens, P95 leitura e P95 confirmação aprovados pela suíte.
- [x] Rollback em dry-run aprovado sem remoção de volumes.

## Go/no-go de homologação externa

- [ ] Credenciais e autorização de deploy recebidas.
- [ ] DNS e TLS públicos validados.
- [ ] SHA backend e SHA frontend implantados registrados.
- [ ] Prometheus carregou regras e Grafana carregou dashboard.
- [ ] Alertas sintéticos entregues ao canal de plantão.
- [ ] Smoke autenticado executado por dois clientes MCP independentes.
- [ ] Feature flags iniciam com escrita desabilitada até aceite.
- [ ] Aceite humano do responsável pelo produto registrado.

## Suporte ao piloto

1. Registrar correlation ID, horário UTC, ferramenta e outcome; nunca coletar token, credencial, payload financeiro completo ou arquivo original.
2. Em escrita incerta, consultar status/journal antes de qualquer nova tentativa.
3. Em cross-account, journal failure ou Unknown, desabilitar writes e acionar Sev0/Sev1.
4. Para rollback, executar `mcp-staging-rollback.ps1` primeiro sem `-Execute`; revisar o plano e somente então executar com autorização.
5. Preservar banco, journals e auditoria durante contenção e rollback.

## Limitações conhecidas

- Homologação externa, DNS/TLS e canais reais de alerta não são reproduzidos pelo harness local.
- O piloto humano e sua taxa de conclusão ainda não ocorreram.
- Política definitiva de retenção e texto jurídico permanecem pendentes dos donos indicados no PRD.
- O compose legado de desenvolvimento não foi alterado; o piloto usa exclusivamente `docker-compose.mcp-staging.yaml`.
