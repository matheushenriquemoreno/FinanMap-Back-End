# Implementation Plan — Custos Fixos

| Field        | Value                        |
| ------------ | ---------------------------- |
| Tech Lead    | @Usuario                     |
| Team         | Solo developer               |
| Epic/Ticket  | Custos Fixos                 |
| Status       | Draft                        |
| Created      | 2026-05-21                   |
| Last Updated | 2026-05-21                   |

## Overview

Implementação da funcionalidade de **Custos Fixos** no FinanMap — um sistema de lembretes de compromissos financeiros recorrentes vinculados a dias de vencimento. O sistema enviará e-mails consolidados 3 dias antes e no dia do vencimento via Resend, com controle de idempotência e opt-out global.

A estratégia de execução segue **vertical slices** (tracer bullet): cada fase entrega uma funcionalidade ponta a ponta, começando pelo CRUD mínimo e crescendo até o processamento de lembretes por e-mail em background.

**Technical Design**: [TECHNICAL-DESIGN.md](file:///d:/GitHub/FinanMap-Back-End/.specs/custos-fixos/TECHNICAL-DESIGN.md)

## Implementation Phases

Estruturado como fatias verticais seguindo a estratégia tracer bullet. Testes (manuais via Scalar/Swagger + logs) estão embutidos em cada fase.

---

### Phase 1: Tracer Bullet — CRUD de Custo Fixo com persistência e endpoint funcional

**Goal**: Um usuário autenticado consegue criar, listar, atualizar e excluir custos fixos pela API. O dado é persistido no MongoDB e recuperável.

**Vertical slice**: Domain → Application → Infra.data (MongoDB) → WebApi (Endpoints)

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| CFIX-P1-01 | Criar entidade `CustoFixo` em `Domain/CustoFixo/Entity/` com validações de domínio (Nome obrigatório, DiaVencimento 1-31, CategoriaId opcional, Ativo default true). Usar `EntityBase` e `DomainValidator` conforme padrão existente. | @Usuario | 1h |
| CFIX-P1-02 | Criar interface `ICustoFixoRepository` em `Domain/CustoFixo/Repository/` estendendo `IRepositoryBase<CustoFixo>` com métodos: `GetByUsuarioId`, `ExisteAtivoDuplicado` (valida unicidade por UsuarioId + Nome + DiaVencimento). | @Usuario | 30min |
| CFIX-P1-03 | Criar `CustoFixoMapping` em `Infra.data/Mongo/Mappings/` implementando `IMongoMapping`. Registrar BsonClassMap com serializer ObjectId para UsuarioId e CategoriaId. Criar índice único composto `{ UsuarioId: 1, Nome: 1, DiaVencimento: 1 }`. | @Usuario | 1h |
| CFIX-P1-04 | Criar `CustoFixoRepository` em `Infra.data/Mongo/Repositorys/` estendendo `RepositoryMongoBase<CustoFixo>` e implementando `ICustoFixoRepository`. Collection name: `CustosFixos`. | @Usuario | 1h |
| CFIX-P1-05 | Criar DTOs no `Application/CustoFixo/DTOs/`: `CreateCustoFixoDTO`, `UpdateCustoFixoDTO`, `CustoFixoResponseDTO`. | @Usuario | 30min |
| CFIX-P1-06 | Criar `ICustoFixoService` e `CustoFixoService` em `Application/CustoFixo/`. Implementar os 4 métodos CRUD: Adicionar (com validação de duplicidade), Listar, Atualizar, Excluir. Usar `IUsuarioLogado` para vincular ao usuário autenticado. Validar categoria usando `ICategoriaRepository.GetById` se CategoriaId for fornecido. | @Usuario | 2h |
| CFIX-P1-07 | Criar controller `CustoFixo.cs` em `WebApi/Controllers/` com endpoints MinimalAPI: `POST /api/custos-fixos`, `GET /api/custos-fixos`, `PUT /api/custos-fixos/{id}`, `DELETE /api/custos-fixos/{id}`. Seguir padrão de `MapGroup` + `RequireAuthorization`. | @Usuario | 1h |
| CFIX-P1-08 | Registrar `MapCustoFixoEndpoints` em `EndpointConfiguration.cs` dentro de `MapProtectedEndpoints`, com tag `"Custos Fixos"`. | @Usuario | 15min |

**Testing**:

- **Manual (Scalar/Swagger)**: Testar cada endpoint CRUD com JWT válido. Validar: criação com campos obrigatórios, rejeição de duplicatas ativas, listagem filtrando por usuário, atualização de campos, exclusão, e que um usuário não acessa custos de outro.
- **Logs**: Verificar que o MongoDB está persistindo na collection `CustosFixos` com os índices criados.

**Acceptance Criteria**:

- [ ] `POST /api/custos-fixos` cria custo fixo e retorna `201` — `CFIX-01`, `CFIX-02`
- [ ] `POST` rejeita duplicata ativa (mesmo nome + dia + usuário) com erro de domínio — `CFIX-03`
- [ ] `GET /api/custos-fixos` retorna lista com status ativo/inativo do usuário logado — `CFIX-05`
- [ ] `PUT /api/custos-fixos/{id}` atualiza nome, dia, categoria e status ativo — `CFIX-04`, `CFIX-06`
- [ ] `DELETE /api/custos-fixos/{id}` exclui o registro — `CFIX-07`
- [ ] Todos os endpoints protegidos por JWT; usuário só manipula seus próprios dados
- [ ] Índice único composto criado no startup da aplicação

**Dependencies**: Nenhuma dependência prévia. A feature usa infra existente (MongoDB, JWT, padrão MinimalAPI).

---

### Phase 2: Processador de Lembretes — Background Service com idempotência

**Goal**: Um Job interno roda periodicamente (8h-17h) e identifica custos fixos que vencem hoje ou em 3 dias, preparando o processamento por usuário com controle de idempotência. O e-mail ainda não é enviado — esta fase garante que a lógica de detecção, agrupamento e idempotência funciona.

**Vertical slice**: Domain (Entidade Idempotência + Enums) → Infra.data (Repository + Mapping) → Application (LembreteProcessor) → WebApi (BackgroundService)

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| CFIX-P2-01 | Criar enum `TipoLembrete` em `Domain/CustoFixo/Enum/` com valores: `DiaDoVencimento`, `Antecedencia`. | @Usuario | 15min |
| CFIX-P2-02 | Criar entidade `CustoFixoLembreteHistorico` em `Domain/CustoFixo/Entity/` com: `UsuarioId`, `DataReferencia` (DateTime), `TipoLembrete` (enum), `CreatedAt` (DateTime). Herdar de `EntityBase`. | @Usuario | 30min |
| CFIX-P2-03 | Criar `ICustoFixoLembreteHistoricoRepository` em `Domain/CustoFixo/Repository/` com métodos: `ExisteRegistroAsync(string usuarioId, DateTime dataReferencia, TipoLembrete tipo)`, `RegistrarEnvioAsync(CustoFixoLembreteHistorico historico)`. | @Usuario | 30min |
| CFIX-P2-04 | Criar `CustoFixoLembreteHistoricoMapping` em `Infra.data/Mongo/Mappings/` com índice único composto `{ UsuarioId: 1, DataReferencia: 1, TipoLembrete: 1 }` e índice TTL de 60 dias em `CreatedAt`. | @Usuario | 1h |
| CFIX-P2-05 | Criar `CustoFixoLembreteHistoricoRepository` em `Infra.data/Mongo/Repositorys/`. Collection: `CustosFixosLembretesHistorico`. Implementar `ExisteRegistroAsync` com filtro direto e `RegistrarEnvioAsync` com InsertOne. | @Usuario | 1h |
| CFIX-P2-06 | Adicionar método `GetCustosFixosAtivosPorDiaVencimento(int diaVencimento)` em `ICustoFixoRepository` e implementá-lo no repository — retorna custos fixos ativos de **todos** os usuários para o dia informado. | @Usuario | 30min |
| CFIX-P2-07 | Adicionar campo `ReceberNotificacoesCustosFixos` (bool, default true) na entidade `Usuario`. Atualizar `UsuarioMapping` para incluir o novo campo. | @Usuario | 30min |
| CFIX-P2-08 | Criar `ICustoFixoLembreteService` e `CustoFixoLembreteService` em `Application/CustoFixo/`. Implementar `ProcessarLembretesAsync()`: calcular "hoje" e "hoje+3" em `America/Sao_Paulo`, tratar dia inexistente no mês (fallback para último dia válido), agrupar custos por usuário, verificar opt-out global (`ReceberNotificacoesCustosFixos`), verificar usuário convidado via `ICompartilhamentoRepository`, checar idempotência, e **logar** o que seria enviado (sem enviar e-mail ainda). | @Usuario | 3h |
| CFIX-P2-09 | Criar `CustoFixoLembreteBackgroundService` em `WebApi/` como `BackgroundService` do ASP.NET. Implementar timer loop: verificar hora atual (8h-17h em `America/Sao_Paulo`), executar `ICustoFixoLembreteService.ProcessarLembretesAsync()`, aguardar 1h, repetir. | @Usuario | 1.5h |
| CFIX-P2-10 | Registrar `CustoFixoLembreteBackgroundService` como `AddHostedService` no `Setup.cs`. | @Usuario | 15min |

**Testing**:

- **Manual**: Verificar logs estruturados indicando quais custos fixos foram detectados, quais usuários seriam notificados, quais foram filtrados por opt-out, convidado ou idempotência.
- **Logs**: Inserir custos fixos de teste com diferentes dias de vencimento. Acompanhar output do BackgroundService. Verificar que o índice TTL está criado corretamente na collection.
- **Cenários de borda**: Testar com dia 31 em mês de 30 dias e em fevereiro. Testar com opt-out ativo. Testar reexecução (idempotência deve impedir duplicata).

**Acceptance Criteria**:

- [ ] BackgroundService inicia automaticamente com a aplicação e roda apenas entre 8h-17h (SP) — `CFIX-08`, `CFIX-09`
- [ ] Custos fixos ativos são detectados para "hoje" e "hoje+3" — `CFIX-08`, `CFIX-09`, `CFIX-19`
- [ ] Dia 31 em fevereiro corretamente ajusta para último dia do mês — `CFIX-13`
- [ ] Registro de idempotência é criado no MongoDB e impede reprocessamento — `CFIX-14`, `CFIX-15`, `CFIX-17`
- [ ] Usuários com opt-out global ativo são ignorados — `CFIX-21`, `CFIX-22`
- [ ] Usuários convidados de conta compartilhada não aparecem no processamento — `CFIX-20`
- [ ] Fuso horário `America/Sao_Paulo` é usado para cálculo da data corrente — `CFIX-18`
- [ ] Logs de `Information` registram cada ciclo; `Warning` para falhas; nenhum dado financeiro exposto

**Dependencies**: Phase 1 completa (entidade `CustoFixo`, repository e CRUD funcional).

---

### Phase 3: Envio de E-mail — Templates HTML e integração com Resend

**Goal**: O processador de lembretes agora envia e-mails consolidados reais via Resend, com templates HTML adequados, assuntos variáveis por tipo de lembrete e fallback em caso de falha.

**Vertical slice**: Application (Template HTML + Email Service) → Infra.data (Resend via `IProvedorEmail`) → produção (e-mail chega na caixa do usuário)

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| CFIX-P3-01 | Criar `CustoFixoLembreteHtmls.cs` em `Application/Email/Htmls/` com método estático para gerar HTML do e-mail consolidado. Template recebe: lista de custos fixos (nome + dias restantes) e tipo de lembrete. Design seguindo o padrão visual do `LoginHtmls`. | @Usuario | 2h |
| CFIX-P3-02 | Criar arquivo de template HTML para o e-mail de lembrete em `Template-emails/lembrete-custo-fixo.html` como referência visual. | @Usuario | 1h |
| CFIX-P3-03 | Criar `ICustoFixoEmailService` e `CustoFixoEmailService` em `Application/Email/` com método `EnviarLembreteAsync(string email, string nomeUsuario, List<CustoFixoLembreteItem> itens, TipoLembrete tipo)`. Usar `IProvedorEmail.EnviarEmail` existente. Assunto variável: "Seus vencimentos estão chegando!" (antecedência) vs "Hoje é dia de vencimento!" (dia do vencimento). | @Usuario | 1.5h |
| CFIX-P3-04 | Integrar `ICustoFixoEmailService` no `CustoFixoLembreteService`: substituir os logs de placeholder pelo envio real. Em caso de sucesso, registrar idempotência. Em caso de falha, logar erro e **não** registrar (permitindo retry na próxima execução). | @Usuario | 1h |

**Testing**:

- **Manual**: Cadastrar custos fixos com vencimento para "hoje" e "hoje+3", executar o job e verificar recebimento do e-mail na caixa de entrada.
- **Cenários**: E-mail consolidado com 1 custo fixo; e-mail consolidado com 3+ custos fixos; falha de envio (simular API key inválida) verificando que o retry funciona na próxima execução.
- **Logs**: Verificar logs de sucesso/erro do `ResendEmailProvedor`.

**Acceptance Criteria**:

- [ ] E-mail consolidado é enviado 3 dias antes do vencimento — `CFIX-08`, `CFIX-10`
- [ ] E-mail consolidado é enviado no dia do vencimento — `CFIX-09`, `CFIX-10`
- [ ] Corpo do e-mail contém nome de cada custo fixo + dias restantes, sem valores financeiros — `CFIX-11`
- [ ] Assunto varia conforme tipo de lembrete — `CFIX-12`
- [ ] Falha de envio é logada e permite nova tentativa na próxima execução — `CFIX-16`
- [ ] Reexecução com sucesso prévio pula silenciosamente (idempotência) — `CFIX-17`

**Dependencies**: Phase 2 completa (processador de lembretes funcional com idempotência).

---

### Phase 4: Opt-out Global e Polimento para Produção

**Goal**: O usuário pode desativar todos os lembretes por e-mail via API. O sistema está pronto para produção com logging adequado, tratamento de erros e documentação atualizada.

**Vertical slice**: Application (configurações de usuário) → WebApi (endpoint de opt-out) → BackgroundService (verificação integrada) → Produção (deploy)

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| CFIX-P4-01 | Criar endpoint `PUT /api/usuarios/configuracoes/custos-fixos` no controller de Usuário existente, recebendo `{ receberNotificacoes: boolean }`. Atualizar o campo `ReceberNotificacoesCustosFixos` do usuário logado. | @Usuario | 1h |
| CFIX-P4-02 | Criar endpoint `GET /api/usuarios/configuracoes/custos-fixos` para o frontend consultar o estado atual do opt-out. | @Usuario | 30min |
| CFIX-P4-03 | Revisar todos os logs do BackgroundService e LembreteService para garantir: nível adequado (`Information` para ciclos normais, `Warning` para falhas recuperáveis, `Error` para falhas críticas), sem dados financeiros, com informações úteis de diagnóstico. | @Usuario | 1h |
| CFIX-P4-04 | Testar fluxo completo end-to-end em ambiente de produção: criar custos fixos, verificar BackgroundService ativo, receber e-mails, testar opt-out, testar idempotência. | @Usuario | 2h |
| CFIX-P4-05 | Atualizar `CONTEXT.md` raiz com referência à feature de Custos Fixos. Documentar o campo `ReceberNotificacoesCustosFixos` adicionado na entidade `Usuario`. | @Usuario | 30min |

**Testing**:

- **Manual end-to-end**: Fluxo completo com Scalar: criar custos fixos → verificar processamento do job → receber e-mail → ativar opt-out → verificar que e-mail para de chegar → desativar opt-out → verificar que e-mail volta.
- **Logs em produção**: Verificar que nenhum log contém dados sensíveis ou financeiros.

**Acceptance Criteria**:

- [ ] Endpoint de opt-out funcional — `CFIX-21`
- [ ] Opt-out ativo impede todos os e-mails — `CFIX-22`
- [ ] Nenhum erro não tratado no BackgroundService (graceful handling)
- [ ] Logs de produção limpos e informativos
- [ ] Documentação atualizada

**Dependencies**: Phase 3 completa (envio de e-mails funcional).

## Milestones

| Milestone | Target Date | Description |
|-----------|-------------|-------------|
| CRUD Funcional | Sem deadline | API completa de Custos Fixos com persistência MongoDB, endpoints protegidos e validação de domínio |
| Processador de Lembretes | Sem deadline | BackgroundService detecta vencimentos, agrupa por usuário, controla idempotência via collection dedicada |
| E-mails em Produção | Sem deadline | Lembretes consolidados enviados via Resend com templates HTML, retry em falhas |
| Feature Completa | Sem deadline | Opt-out global funcional, logs de produção revisados, documentação atualizada |

## Dependencies

| Dependency | Type | Owner | Status | Risk if Delayed |
|------------|------|-------|--------|-----------------|
| MongoDB (Atlas / container) | Technical | @Usuario | Disponível | Nenhuma fase pode iniciar sem banco |
| Resend API Key | External | @Usuario | Disponível | Phase 3 bloqueada se API key não estiver configurada |
| Cron Externo (ping wake-up) | External | @Usuario | Disponível | BackgroundService não executa se a API não for acordada |
| Infra de e-mail existente (`IProvedorEmail`, `ResendEmailProvedor`) | Internal | @Usuario | Disponível | Phase 3 depende dessa infra |
| Entidade `Usuario` existente | Internal | @Usuario | Disponível | Phase 2 adicionará campo; se houver mudanças conflitantes, merge pode ser necessário |

## Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Cold start impede execução do job** — a API pode não acordar a tempo e o BackgroundService nunca executa no dia | Alto | Média | O cron externo já está configurado. O intervalo de 1h entre tentativas (8h-17h) dá 10 oportunidades/dia. Monitorar logs para verificar execuções. |
| **Duplicação de envio por race condition** — se a aplicação reiniciar durante processamento, o registro de idempotência pode não ter sido salvo | Alto | Baixa | O índice único no MongoDB garante idempotência em nível de banco. Se tentar inserir duplicata, o MongoDB rejeita com `DuplicateKeyException`, tratado no código. |
| **Dias inexistentes no mês causam erros** — ex: custo fixo com dia 31 em fevereiro | Médio | Alta | Lógica de fallback implementada na Phase 2 (CFIX-P2-08): usar `DateTime.DaysInMonth()` para ajustar ao último dia válido. |
| **Resend indisponível ou rate limit** — falha ao enviar e-mail via Resend | Médio | Baixa | Falhas não registram idempotência, permitindo retry automático na próxima execução do job. Logs de `Error` alertam o desenvolvedor. |
| **Mudanças na entidade Usuario quebram serialização** — adicionar `ReceberNotificacoesCustosFixos` em documentos existentes que não têm o campo | Baixo | Alta | Definir default `true` na entidade. MongoDB não requer migration, mas documentos antigos retornarão `false`/`null` se o default não for tratado. Usar `BsonDefaultValue(true)` no mapping. |

## Testing Strategy

O projeto não possui suíte de testes automatizados. A estratégia de testes para Custos Fixos será integralmente **manual**, utilizando as ferramentas já disponíveis.

### Ferramentas
- **Scalar/Swagger**: Para testar endpoints da API com JWT válido.
- **MongoDB Compass/Atlas UI**: Para inspecionar collections, índices e dados persistidos.
- **Logs do Console**: Para monitorar a execução do BackgroundService, processamento de lembretes e envios de e-mail.
- **Caixa de e-mail real**: Para verificar recebimento, formato e conteúdo dos e-mails.

### Cenários Críticos por Fase

**Phase 1 — CRUD**:
1. Criar custo fixo com campos válidos → 201 Created
2. Criar custo fixo duplicado (mesmo nome + dia + usuário ativo) → erro de domínio
3. Listar custos fixos de usuário A → não retorna custos de usuário B
4. Atualizar status ativo/inativo → reflete no GET
5. Excluir custo fixo → não aparece mais no GET

**Phase 2 — Processador**:
1. Custo fixo com vencimento "hoje" aparece no log como candidato a lembrete
2. Custo fixo com vencimento "daqui a 3 dias" aparece no log
3. Custo fixo inativo NÃO aparece
4. Dia 31 em mês com 30 dias → ajustado para dia 30
5. Dia 29, 30 ou 31 em fevereiro → ajustado para 28 (ou 29 em bissexto)
6. Reexecução não gera registro duplicado no histórico
7. Usuário com opt-out ativo → ignorado
8. Usuário convidado de conta compartilhada → ignorado

**Phase 3 — E-mail**:
1. E-mail chega na caixa de entrada com assunto correto para cada tipo
2. E-mail consolida múltiplos custos fixos em um único corpo
3. E-mail NÃO contém valores financeiros
4. Falha simulada (API key inválida) → log de erro, sem registro de idempotência, retry funciona

**Phase 4 — Opt-out**:
1. PUT opt-out → campo atualizado no MongoDB
2. GET opt-out → retorna estado atual
3. Fluxo completo: criar custo → receber e-mail → ativar opt-out → não receber e-mail → desativar → receber novamente

## Rollback Plan

### Deployment Strategy

Deploy direto, sem feature flags. A funcionalidade fica ativa imediatamente após o deploy.

### Rollback Triggers

| Trigger | Threshold | Action |
|---------|-----------|--------|
| E-mails duplicados detectados | > 1 usuário reporta duplicata | Rollback imediato |
| BackgroundService causando crash ou instabilidade na API | Qualquer incidente | Rollback imediato |
| Erro massivo no Resend (rate limit, bloqueio) | > 50% de falhas em 1 ciclo | Desabilitar job via variável de ambiente |

### Rollback Steps

1. **Imediato**: Reverter o deploy para a versão anterior (sem Custos Fixos). O BackgroundService para junto com a versão antiga.
2. **Banco de dados**: Não é necessário remover collections — dados órfãos não causam problema. O campo `ReceberNotificacoesCustosFixos` na collection `Usuario` será ignorado pela versão sem a feature.
3. **Comunicação**: Se e-mails indevidos foram enviados, avaliar necessidade de comunicação ao usuário afetado.

### Post-Rollback

1. RCA: Identificar causa raiz do problema.
2. Fix: Corrigir o código.
3. Re-teste: Validar em ambiente local com cenários que causaram o problema.
4. Re-deploy: Nova tentativa com fix aplicado.
