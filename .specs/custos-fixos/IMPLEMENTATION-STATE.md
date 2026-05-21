# Implementation State: custos-fixos

## Phase 1 -- Tracer Bullet: CRUD de Custo Fixo com persistencia e endpoint funcional
- [x] Task 1: Criar entidade `CustoFixo` com validacoes de dominio.
- [x] Task 2: Criar interface `ICustoFixoRepository`.
- [x] Task 3: Criar `CustoFixoMapping` com serializers ObjectId e indice unico composto.
- [x] Task 4: Criar `CustoFixoRepository`.
- [x] Task 5: Criar DTOs de criacao, atualizacao e resposta.
- [x] Task 6: Criar `ICustoFixoService` e `CustoFixoService` com CRUD.
- [x] Task 7: Criar controller `CustoFixo` com endpoints Minimal API.
- [x] Task 8: Registrar `MapCustoFixoEndpoints` nos endpoints protegidos.

**Status: completed**

**Validation:** `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln` passou em 2026-05-21. `dotnet format --verify-no-changes` passou para os arquivos da feature; a verificacao global ainda aponta whitespace legado fora do escopo desta fase.

## Phase 2 -- Processador de Lembretes: Background Service com idempotencia
- [ ] Task 1: Criar enum `TipoLembrete`.
- [ ] Task 2: Criar entidade `CustoFixoLembreteHistorico`.
- [ ] Task 3: Criar repository de historico.
- [ ] Task 4: Criar mapping do historico com indice unico e TTL.
- [ ] Task 5: Implementar repository de historico.
- [ ] Task 6: Buscar custos fixos ativos por dia de vencimento.
- [ ] Task 7: Adicionar opt-out global em `Usuario`.
- [ ] Task 8: Criar processador de lembretes.
- [ ] Task 9: Criar BackgroundService.
- [ ] Task 10: Registrar HostedService.

## Phase 3 -- Envio de E-mail: Templates HTML e integracao com Resend
- [ ] Task 1: Criar HTML gerador de lembrete.
- [ ] Task 2: Criar template de referencia.
- [ ] Task 3: Criar service de e-mail de custo fixo.
- [ ] Task 4: Integrar envio real ao processador.

## Phase 4 -- Opt-out Global e Polimento para Producao
- [ ] Task 1: Criar endpoint PUT de configuracao de custos fixos.
- [ ] Task 2: Criar endpoint GET de configuracao de custos fixos.
- [ ] Task 3: Revisar logs do processador.
- [ ] Task 4: Testar fluxo completo end-to-end.
- [ ] Task 5: Atualizar documentacao raiz.
