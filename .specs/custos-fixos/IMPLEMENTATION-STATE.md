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
- [x] Task 1: Criar enum `TipoLembrete`.
- [x] Task 2: Criar entidade `CustoFixoLembreteHistorico`.
- [x] Task 3: Criar repository de historico.
- [x] Task 4: Criar mapping do historico com indice unico e TTL.
- [x] Task 5: Implementar repository de historico.
- [x] Task 6: Buscar custos fixos ativos por dia de vencimento.
- [x] Task 7: Adicionar opt-out global em `Usuario`.
- [x] Task 8: Criar processador de lembretes.
- [x] Task 9: Criar BackgroundService.
- [x] Task 10: Registrar HostedService.

**Status: completed**

**Validation:** `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln` passou com sucesso em 2026-05-21 após resolver o conflito de namespace em `CustoFixoLembreteService` e adicionar `using WebApi` no `Program.cs`.

## Phase 3 -- Envio de E-mail: Templates HTML e integracao com Resend
- [x] Task 1: Criar HTML gerador de lembrete.
- [x] Task 2: Criar template de referencia.
- [x] Task 3: Criar service de e-mail de custo fixo.
- [x] Task 4: Integrar envio real ao processador.

**Status: completed**

**Validation:** `dotnet build` compilado com sucesso e `dotnet format` executado sem erros na solução. Rastreabilidade de requisitos validada.

## Phase 4 -- Opt-out Global e Polimento para Producao
- [x] Task 1: Criar endpoint PUT de configuracao de custos fixos.
- [x] Task 2: Criar endpoint GET de configuracao de custos fixos.
- [x] Task 3: Revisar logs do processador.
- [x] Task 4: Testar fluxo completo end-to-end.
- [x] Task 5: Atualizar documentacao raiz.

**Status: completed**

**Validation:** `dotnet build` compilado com sucesso com 0 avisos e 0 erros. `dotnet format` executado limpando warnings legados. Modificações do opt-out global integradas ao CONTEXT.md raiz. Rastreabilidade de requisitos validada.
