# Implementation Plan — MCP Financeiro Conversacional

| Field | Value |
| --- | --- |
| Tech Lead | Você |
| Team | Você + Codex |
| Epic/Ticket | N/A |
| Status | Draft |
| Created | 2026-07-25 |
| Last Updated | 2026-07-25 |
| Start | 2026-07-25 |
| Target | 2026-07-31 |
| Contingency | 2026-08-01 a 2026-08-02, somente se um bloqueador crítico impedir a homologação |

## Overview

Este plano implementa integralmente o MCP Financeiro Conversacional em duas branches com o mesmo nome, `feature/mcp-financeiro-conversacional`, nos repositórios backend e frontend do FinanMap. A execução usa fatias verticais: cada fase entrega um fluxo técnico completo e validado, mas nenhuma fase intermediária será liberada para testes com usuários. O piloto começa somente depois de todas as funcionalidades, validações técnicas e publicação em homologação estarem concluídas.

O cronograma de 25 a 31/07/2026 é agressivo para 125 requisitos P0/P1 e uma única frente de trabalho composta pelo usuário e Codex. O plano estima aproximadamente **81 horas de esforço assistido**. O prazo depende de decisões de autenticação concluídas no primeiro dia, acesso ao ambiente de homologação e ausência de regressões relevantes nos serviços financeiros existentes.

**PRD**: [PRODUCT-REQUIREMENTS.md](./PRODUCT-REQUIREMENTS.md)

**Technical Design**: [TECHNICAL-DESIGN.md](./TECHNICAL-DESIGN.md). O desenho fixa a revisão MCP `2025-11-25`, o SDK C# estável `1.4.1`, OAuth 2.1 com PKCE, prévias de 15 minutos, contratos de ferramentas e o modelo de consistência para MongoDB standalone: journal antes do efeito, operações atômicas por documento, idempotência, leases, marcadores e reconciliação.

## Repository and Workstream Boundaries

| Workstream | Repository | Branch | Responsibilities |
| --- | --- | --- | --- |
| Backend | `D:\FinamMap\FinanMap-Back-End` | `feature/mcp-financeiro-conversacional` | Transporte MCP remoto, autenticação, ferramentas, consultas, prévias, confirmações, importações, persistência, auditoria, segurança, métricas e endpoints consumidos pelo frontend |
| Frontend | `D:\FinamMap\FinanMap-Front-End` | `feature/mcp-financeiro-conversacional` | Configuração e revogação da conexão, instruções, prompts-base, consentimento, status, histórico, detalhes de falhas e experiência responsiva |
| Shared | Ambos | `feature/mcp-financeiro-conversacional` | Contratos, fixtures, testes ponta a ponta, homologação, documentação operacional e aceite |

### Existing Integration Points

**Backend**

- `WebApi/Program.cs`: pipeline ASP.NET Core, JWT, CORS, health checks e registro do endpoint MCP.
- `WebApi/Configs/EndpointConfiguration.cs`: endpoints protegidos de configuração, histórico e suporte ao frontend.
- `Application/*/Service`: regras existentes de categorias, rendimentos, despesas, investimentos e custos fixos que devem ser reutilizadas.
- `Domain/Login/Interfaces/IUsuarioLogado.cs` e `WebApi/Interceptor/UsuarioLogado.cs`: identidade autenticada e isolamento da conta individual.
- `Infra.data/Mongo`: persistência de conexões, prévias, importações e auditoria.
- `Tests`: extensão da suíte xUnit com testes de unidade, integração, contrato e autorização.

**Frontend**

- `src/components/Configuracoes/ModalConfiguracoes.vue`: inclusão da área “Integração com IA”.
- `src/services/api/AxiosHelper.ts`: consumo autenticado dos endpoints de configuração e histórico.
- `src/services`: novo serviço dedicado ao MCP.
- `src/Model`: tipos de conexão, auditoria, status e prompts.
- `src/layouts/MainLayout.vue`: acesso à configuração existente, sem criar navegação paralela.
- `scripts` e `package.json`: testes comportamentais e gates de build.

## Planning Assumptions and Gates

| Gate | Default Used by This Plan | Deadline | Consequence if Unresolved |
| --- | --- | --- | --- |
| Transporte remoto | Streamable HTTP da revisão MCP `2025-11-25` | Resolved | Bloqueia interoperabilidade e smoke test com clientes |
| SDK oficial .NET | `ModelContextProtocol.AspNetCore` estável `1.4.1`; não adotar prerelease na V1 | Resolved | Risco de quebra de API durante a semana |
| Autorização remota | OAuth 2.1 Authorization Code + PKCE `S256`, discovery, scopes, audience e revogação imediata | Resolved | Bloqueia conexão segura com agentes genéricos |
| Validade de prévia | Valor configurável; default técnico inicial de 15 minutos sujeito a validação de Produto e Segurança | 27/07 | Pode gerar confirmação sobre dados desatualizados |
| Retenção da auditoria | Configurável e sem rotina destrutiva automática até definição formal de Produto/Privacidade | 30/07 | Não bloqueia homologação, mas bloqueia produção |
| Limite de importação | 1.000 itens na V1, conforme critério mínimo do PRD | 29/07 | Falha no critério de aceite de importação |
| Consistência MongoDB | MongoDB standalone em todos os ambientes, sem transações multi-documento; usar journal canônico, compare-and-set, effect markers e reconciliação | Resolved | Implementação não pode depender de replica set ou atomicidade entre coleções |
| Homologação | URL HTTPS, segredos, MongoDB, acesso ao deploy e configuração de CORS disponíveis | 30/07 | Bloqueia a meta de disponibilizar a solução para o piloto |

## Execution Rules

- Cada fase termina com testes, revisão de diff e commit separado por repositório afetado.
- A habilidade `commit` deve ser usada quando os commits forem efetivamente criados.
- A alteração preexistente em `WebApi/Properties/launchSettings.json` não pertence a este plano e não deve ser restaurada, sobrescrita, adicionada ao stage ou incluída em commits sem autorização específica.
- Falha em um gate P0 interrompe o avanço para a fase dependente.
- O frontend não reimplementa regras financeiras; ele consome contratos do backend.
- As ferramentas MCP reutilizam serviços de aplicação existentes; não escrevem diretamente nas coleções financeiras.
- Nenhuma fase pode introduzir transação multi-documento ou exigir replica set.
- Toda escrita cria o journal canônico antes do efeito; falha nessa persistência bloqueia a execução.
- Operações multi-documento são passos idempotentes retomáveis, não uma unidade atômica.
- Nenhum arquivo Excel, CSV ou conteúdo binário entra no backend.
- Nenhum teste com usuário ocorre antes da conclusão da Fase 7.

## Implementation Phases

### Phase 1 — Tracer Bullet Interno: conexão, leitura de categorias e auditoria

**Target date**: 2026-07-25

**Goal**: Provar internamente o fluxo completo em homologação local: o titular configura uma conexão, um cliente MCP autenticado descobre as ferramentas, consulta categorias e encontra a operação no histórico do FinanMap.

**Vertical slice**: Configuração frontend → API de conexão → autorização MCP → transporte remoto → ferramenta de leitura → serviço de categorias → auditoria MongoDB → histórico frontend.

**Requirements covered**: `MCP-01` a `MCP-05`, `MCP-08` a `MCP-15`, `MCP-17`, `MCP-98` a `MCP-111`, `MCP-114`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P1-01` | Shared | Registrar a versão estável do protocolo, SDK C# e fluxo de autorização adotados, incluindo compatibilidade mínima e estratégia de atualização | Você + Codex | 0.5h |
| `MCPF-P1-02` | Backend | Adicionar configurações e feature flags independentes para endpoint MCP, ferramentas de escrita e histórico | Você + Codex | 0.5h |
| `MCPF-P1-03` | Backend | Integrar o servidor MCP remoto ao ASP.NET Core com descoberta de capacidades, Streamable HTTP, validação de `Origin` e health signal | Você + Codex | 1.5h |
| `MCPF-P1-04` | Backend | Implementar autorização, conexão, status e revogação individual, reutilizando login/cadastro por e-mail e código sem criar usuário via ferramenta MCP | Você + Codex | 1.5h |
| `MCPF-P1-05` | Backend | Vincular toda chamada MCP ao titular autenticado sem aceitar `X-Proprietario-Id` ou identidade informada pelo agente | Você + Codex | 1.5h |
| `MCPF-P1-06` | Backend | Criar o journal canônico antes de cada chamada para conexão, revogação e ferramenta, com status, correlation ID e redação de segredos | Você + Codex | 1.25h |
| `MCPF-P1-07` | Backend | Expor a ferramenta read-only de consulta de categorias reutilizando `ICategoriaService` | Você + Codex | 0.75h |
| `MCPF-P1-08` | Frontend | Criar modelos e serviço HTTP para status, criação, revogação e histórico MCP | Você + Codex | 0.75h |
| `MCPF-P1-09` | Frontend | Adicionar a seção “Integração com IA” ao modal de configurações com estado vazio, status e revogação | Você + Codex | 1.5h |
| `MCPF-P1-10` | Frontend | Apresentar endpoint, instrução mínima de conexão, consentimento sobre processamento externo e feedback de cópia | Você + Codex | 0.75h |
| `MCPF-P1-11` | Shared | Adicionar testes de contrato, autorização, revogação, auditoria e componente; executar smoke test com ferramenta oficial de inspeção MCP | Você + Codex | 1.5h |
| `MCPF-P1-12` | Shared | Revisar diffs, preservar mudanças não relacionadas e criar commits separados de backend e frontend | Você + Codex | 0.25h |

**Testing**:

- Backend unit: conexão ativa, revogada e expirada.
- Backend integration: descoberta MCP, chamada autenticada e bloqueio após revogação.
- Registration: usuário existente autoriza; usuário novo conclui o cadastro atual e retorna ao consentimento pendente.
- Authorization: tentativa com usuário diferente, identificador alheio e `X-Proprietario-Id`.
- Failure injection: falha ao criar o journal impede a execução da ferramenta.
- Contract: schema da ferramenta de categorias e resposta de erro.
- Frontend component: estados desconectado, conectado, revogado, carregando e falha.
- Smoke: conexão real com inspector/cliente MCP contra ambiente local.

**Acceptance Criteria**:

- [ ] Um cliente MCP compatível descobre a ferramenta de categorias.
- [ ] A ferramenta retorna apenas categorias do titular autenticado.
- [ ] Uma conexão revogada é rejeitada na chamada seguinte.
- [ ] Login ou cadastro FinanMap retorna à autorização pendente sem expor token ao frontend.
- [ ] A chamada aparece no histórico do usuário.
- [ ] Nenhuma chamada executa se o journal inicial não puder ser persistido.
- [ ] O frontend não expõe a credencial após a etapa segura definida no contrato.
- [ ] Nenhum contexto compartilhado é aceito pelo fluxo MCP.
- [ ] Testes da fase passam nos dois repositórios.

**Dependencies**: Gates de transporte, SDK e autorização aprovados. MongoDB local disponível.

**Commit checkpoint**:

- Backend: `feat(mcp): establish authenticated read-only tracer`
- Frontend: `feat(mcp): add connection setup tracer`

---

### Phase 2 — Consultas financeiras e dados para análise

**Target date**: 2026-07-26

**Goal**: Permitir que qualquer agente compatível consulte os cinco domínios financeiros, totais, maiores movimentos, distribuição por categoria e comparação de períodos sem confirmação.

**Vertical slice**: Ferramentas read-only → serviços/repositórios financeiros → filtros e agregações → respostas estruturadas → auditoria → prompts e histórico no frontend.

**Requirements covered**: `MCP-06`, `MCP-07`, `MCP-16`, `MCP-18` a `MCP-33`, `MCP-115` a `MCP-121`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P2-01` | Backend | Criar serviço de consulta MCP com período, categoria, descrição, paginação e limites consistentes | Você + Codex | 1.25h |
| `MCPF-P2-02` | Backend | Expor ferramentas read-only para receitas, despesas, investimentos e custos fixos | Você + Codex | 1.75h |
| `MCPF-P2-03` | Backend | Expor ferramentas de totais, maiores movimentos, distribuição por categoria e comparação entre períodos | Você + Codex | 1.25h |
| `MCPF-P2-04` | Backend | Padronizar respostas com período, moeda, filtros, paginação, estado vazio e indicação de resultados adicionais | Você + Codex | 1h |
| `MCPF-P2-05` | Backend | Aplicar minimização de dados e redação de parâmetros sensíveis na auditoria das consultas | Você + Codex | 0.75h |
| `MCPF-P2-06` | Frontend | Completar dicas e prompts-base de consultas e análises, com exemplos copiáveis em pt-BR | Você + Codex | 1.5h |
| `MCPF-P2-07` | Shared | Criar fixtures reconciliáveis e testar filtros, agregações, paginação, estado vazio, isolamento e erros | Você + Codex | 1.25h |
| `MCPF-P2-08` | Shared | Revisar diffs e criar commits separados da fase | Você + Codex | 0.25h |

**Testing**:

- Unit: normalização de períodos, paginação e limites.
- Integration: uma ferramenta por domínio, filtros combinados e conta sem dados.
- Reconciliation: totais das ferramentas iguais aos registros-fixture.
- Security: nenhuma resposta inclui dados de outra conta.
- Contract: nomes, descrições e schemas marcados como leitura.
- Frontend: renderização e cópia de dicas/prompts em desktop e mobile.

**Acceptance Criteria**:

- [ ] As cinco famílias de dados podem ser consultadas por um agente.
- [ ] Totais e comparações reconciliam 100% com as fixtures.
- [ ] Consultas não exigem confirmação.
- [ ] Respostas extensas informam paginação sem omissão silenciosa.
- [ ] O histórico registra ferramenta, filtros, data, origem e resultado.
- [ ] Todos os prompts-base de leitura estão visíveis no frontend.

**Dependencies**: Fase 1 concluída. Contratos de data e moeda validados contra o domínio atual.

**Commit checkpoint**:

- Backend: `feat(mcp): expose financial read tools`
- Frontend: `feat(mcp): add query guides and prompts`

---

### Phase 3 — Motor de prévia e confirmação com categorias e receitas

**Target date**: 2026-07-27

**Goal**: Implementar a fronteira de segurança de escrita e comprovar criação, alteração e exclusão definitiva para categorias e receitas.

**Vertical slice**: Solicitação MCP → validação → prévia persistida → apresentação pelo agente → confirmação de uso único → serviço de aplicação → resultado → auditoria → histórico detalhado no frontend.

**Requirements covered**: `MCP-34` a `MCP-55`, `MCP-65` a `MCP-71`, `MCP-122` a `MCP-124`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P3-01` | Backend | Criar modelo persistido de prévia com titular, ação, snapshot, versão, expiração, status e identificador idempotente | Você + Codex | 1.5h |
| `MCPF-P3-02` | Backend | Criar `McpOperationJournal` canônico com índice idempotente, estados, passos embutidos, lease, tentativas e resultado auditável | Você + Codex | 1.5h |
| `MCPF-P3-03` | Backend | Implementar reserva condicional de prévia, confirmação de uso único, expiração e detecção de mudança concorrente | Você + Codex | 2h |
| `MCPF-P3-04` | Backend | Expor ferramentas de preparar e confirmar criação, alteração e exclusão de categorias | Você + Codex | 1.25h |
| `MCPF-P3-05` | Backend | Expor ferramentas de preparar e confirmar criação, alteração e exclusão de receitas | Você + Codex | 1.25h |
| `MCPF-P3-06` | Backend | Implementar effect markers para criação/alteração, remoção condicional, consulta segura de resultado e reconciliador de leases expirados | Você + Codex | 1.5h |
| `MCPF-P3-07` | Backend | Garantir que o journal contenha snapshot resumido, confirmação, executor, resultado e rejeição sem segredo ou dual-write de auditoria | Você + Codex | 0.75h |
| `MCPF-P3-08` | Frontend | Exibir no histórico detalhes seguros de prévia, confirmação, reconciliação, expiração e resultado de escrita | Você + Codex | 1.25h |
| `MCPF-P3-09` | Shared | Testar confirmação ausente/repetida/expirada, concorrência, cross-account, exclusão e interrupções em cada janela journal/efeito | Você + Codex | 3.5h |
| `MCPF-P3-10` | Shared | Revisar diffs e criar commits separados da fase | Você + Codex | 0.25h |

**Testing**:

- Unit: máquinas de estado da prévia/journal, lease e confirmação de uso único.
- Integration: CRUD confirmado de categoria e receita usando serviços existentes.
- Concurrency: registro alterado entre prévia e confirmação.
- Idempotency: repetição antes, durante e depois do resultado.
- Failure injection: interrupção antes/depois da reserva, criação, alteração, exclusão e finalização do journal.
- Reconciliation: effect marker comprova criação/alteração; exclusão inconclusiva não sofre retry cego.
- Security: confirmação ligada a outro usuário ou outra prévia.
- Frontend: detalhes de confirmação, expiração e falha.

**Acceptance Criteria**:

- [ ] Preparar nunca altera dados definitivos.
- [ ] Confirmar uma vez executa exatamente a ação apresentada.
- [ ] Confirmar novamente não duplica nem repete a ação.
- [ ] Journal é persistido antes de qualquer efeito financeiro.
- [ ] Operações interrompidas são retomadas com o mesmo operation ID ou classificadas como `Unknown`.
- [ ] Alteração concorrente invalida a prévia.
- [ ] Exclusão apresenta aviso de irreversibilidade.
- [ ] Categorias e receitas possuem CRUD completo via MCP.
- [ ] Auditoria permanece disponível após exclusão.

**Dependencies**: Fases 1 e 2 concluídas. Gate de validade da prévia definido. Índices únicos e compare-and-set validados no MongoDB standalone.

**Commit checkpoint**:

- Backend: `feat(mcp): add single-use confirmation engine`
- Frontend: `feat(mcp): show confirmed operation history`

---

### Phase 4 — Escritas completas de despesas, investimentos e custos fixos

**Target date**: 2026-07-28

**Goal**: Completar o CRUD conversacional dos cinco domínios financeiros com validações e erros acionáveis.

**Vertical slice**: Ferramentas de escrita → motor de prévia/confirmar → validações específicas → serviços existentes → exclusão definitiva ou bloqueio de domínio → auditoria → prompts e histórico.

**Requirements covered**: `MCP-56` a `MCP-64`, complementos de `MCP-65` a `MCP-71`, `MCP-115` a `MCP-124`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P4-01` | Backend | Implementar ferramentas confirmadas de criar, alterar e excluir despesas, respeitando agrupamentos e regras atuais | Você + Codex | 1.5h |
| `MCPF-P4-02` | Backend | Implementar ferramentas confirmadas de criar, alterar e excluir investimentos | Você + Codex | 1h |
| `MCPF-P4-03` | Backend | Implementar ferramentas confirmadas de criar, alterar e excluir custos fixos mensais | Você + Codex | 1.25h |
| `MCPF-P4-04` | Backend | Padronizar validações de campos obrigatórios, valor, data, recorrência, categoria e ambiguidade | Você + Codex | 1h |
| `MCPF-P4-05` | Backend | Converter bloqueios de domínio e dependências em erros estruturados e acionáveis | Você + Codex | 0.75h |
| `MCPF-P4-06` | Backend | Modelar agrupamentos/recorrências como passos idempotentes no journal e garantir exclusões definitivas sem rollback transacional ou restauração | Você + Codex | 1.25h |
| `MCPF-P4-07` | Frontend | Adicionar prompts-base de criação, alteração e exclusão para os cinco domínios | Você + Codex | 1.25h |
| `MCPF-P4-08` | Shared | Executar matriz de CRUD, validação, dependência, concorrência, idempotência e auditoria em todos os domínios | Você + Codex | 2.25h |
| `MCPF-P4-09` | Shared | Revisar diffs e criar commits separados da fase | Você + Codex | 0.25h |

**Testing**:

- Unit: validadores e mapeadores por domínio.
- Integration: CRUD confirmado para despesa, investimento e custo fixo.
- Multi-document: interrupção em agrupamento/recorrência retoma apenas passos pendentes.
- Regression: operações tradicionais continuam usando as mesmas regras.
- Destructive flow: exclusão definitiva, dependência bloqueada e aviso.
- Contract: erro de campo ausente ou ambíguo permite nova pergunta do agente.
- Frontend: prompts e histórico para todos os tipos de escrita.

**Acceptance Criteria**:

- [ ] Categorias, receitas, despesas, investimentos e custos fixos têm CRUD MCP completo.
- [ ] Toda escrita usa o mesmo motor de prévia e confirmação.
- [ ] Ambiguidades retornam campos específicos para esclarecimento.
- [ ] Regras financeiras existentes não são contornadas.
- [ ] Exclusões confirmadas são definitivas.
- [ ] Operações multi-documento convergem por passos idempotentes sem exigir transação MongoDB.
- [ ] Erros de domínio são compreensíveis pelo usuário.

**Dependencies**: Fase 3 concluída. Serviços existentes validados para conta individual.

**Commit checkpoint**:

- Backend: `feat(mcp): complete confirmed financial writes`
- Frontend: `feat(mcp): document financial write prompts`

---

### Phase 5 — Importação estruturada com sucesso parcial e correção

**Target date**: 2026-07-29

**Goal**: Importar até 1.000 itens estruturados pelo agente, gravar os válidos, devolver falhas acionáveis e impedir duplicidades sem decisão explícita.

**Vertical slice**: Dados estruturados do agente → rascunho → validação item a item → categoria/duplicidade → prévia agregada → confirmação → serviços financeiros → sucesso parcial → histórico e guia frontend.

**Requirements covered**: `MCP-72` a `MCP-97`, complementos de `MCP-98` a `MCP-105`, `MCP-115` a `MCP-124`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P5-01` | Backend | Criar modelos e persistência de lote/item com referência de origem, status, resultado e operation ID por item | Você + Codex | 1.5h |
| `MCPF-P5-02` | Backend | Implementar preparação de lote heterogêneo e validação independente por item | Você + Codex | 1.5h |
| `MCPF-P5-03` | Backend | Implementar correspondência inequívoca, sugestão e criação proposta de categorias | Você + Codex | 1.5h |
| `MCPF-P5-04` | Backend | Implementar detecção de possível duplicidade e bloqueio até decisão explícita | Você + Codex | 1.5h |
| `MCPF-P5-05` | Backend | Gerar prévia com contagens, totais por tipo, valores propostos e motivos acionáveis | Você + Codex | 1h |
| `MCPF-P5-06` | Backend | Confirmar itens válidos com journal idempotente por item, sucesso parcial, reconciliação, resultado e reenvio corrigido | Você + Codex | 2.75h |
| `MCPF-P5-07` | Backend | Rejeitar conteúdo binário, limitar payload e garantir que nenhum documento seja persistido ou logado | Você + Codex | 0.75h |
| `MCPF-P5-08` | Frontend | Adicionar guia de importação, prompts-base, explicação de privacidade e leitura do histórico por lote | Você + Codex | 1.25h |
| `MCPF-P5-09` | Shared | Testar Excel/CSV representados por payloads, 1.000 itens, mistura de tipos, duplicidade, falha parcial, correção e reenvio | Você + Codex | 3h |
| `MCPF-P5-10` | Shared | Revisar diffs e criar commits separados da fase | Você + Codex | 0.25h |

**Testing**:

- Unit: normalização, correspondência de categoria e detector de duplicidade.
- Integration: preparação e confirmação com todos os tipos.
- Load: lote com 1.000 itens dentro do limite definido.
- Partial success: válidos persistidos, inválidos intactos e motivos por item.
- Retry: somente itens corrigidos, sem duplicar sucessos anteriores.
- Failure injection: interrupção após itens distintos, categoria proposta e atualização do resumo do lote.
- Reconciliation: lote agrega estados dos itens sem presumir atomicidade ou reexecutar concluídos.
- Privacy: arquivos/binários recusados e payload integral ausente dos logs.
- Frontend: guia, prompts e detalhe do lote acessíveis em mobile.

**Acceptance Criteria**:

- [ ] Um lote de 1.000 itens pode ser preparado.
- [ ] Todos os itens possuem referência de origem.
- [ ] Possível duplicidade permanece bloqueada até decisão.
- [ ] Itens válidos são importados mesmo quando outros falham.
- [ ] Cada falha retorna motivo e orientação de correção.
- [ ] Reenvio não duplica itens já importados.
- [ ] Uma interrupção mantém cada item como concluído, pendente, falho ou desconhecido de forma verificável.
- [ ] O backend não recebe nem armazena documentos.
- [ ] O histórico mostra resumo e resultado do lote.

**Dependencies**: Fase 4 concluída. Índices e limite de payload definidos.

**Commit checkpoint**:

- Backend: `feat(mcp): add confirmed partial batch imports`
- Frontend: `feat(mcp): add import guidance and history`

---

### Phase 6 — Segurança, observabilidade, desempenho e UX de produção

**Target date**: 2026-07-30

**Goal**: Fechar os requisitos transversais para tornar a implementação completa segura, observável, responsiva e operacional antes da homologação.

**Vertical slice**: Cliente MCP/frontend → limites e autorização → ferramentas → métricas/logs/auditoria → estados operacionais no frontend → testes de segurança, contrato e desempenho.

**Requirements covered**: `MCP-112`, `MCP-113`, `MCP-115` a `MCP-125` e endurecimento de todos os requisitos anteriores.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P6-01` | Backend | Concluir conformidade de autorização, validação de origem, CORS, rate limiting, revogação e respostas sem vazamento de existência | Você + Codex | 1.25h |
| `MCPF-P6-02` | Backend | Revisar descrições e schemas de todas as ferramentas, separando leitura de escrita de modo inequívoco | Você + Codex | 0.75h |
| `MCPF-P6-03` | Backend | Adicionar correlation ID, métricas de journal/lease/reconciliação, logs estruturados e alertas de operação travada ou desconhecida | Você + Codex | 1.5h |
| `MCPF-P6-04` | Backend | Criar índices e otimizar paginação, consultas, prévias e importações para os gates de latência | Você + Codex | 1.25h |
| `MCPF-P6-05` | Frontend | Finalizar estados vazios, loading, erro, revogação, indisponibilidade, filtros de histórico e detalhes seguros | Você + Codex | 1.25h |
| `MCPF-P6-06` | Frontend | Validar acessibilidade, responsividade, dark mode, foco, cópia de credenciais e aviso de processamento externo | Você + Codex | 1.25h |
| `MCPF-P6-07` | Shared | Executar suíte de conformidade MCP, testes de contrato e smoke com pelo menos dois clientes independentes compatíveis | Você + Codex | 1.5h |
| `MCPF-P6-08` | Shared | Executar testes de segurança, fault injection, reconciliação, latência P95 e regressão completa dos dois repositórios | Você + Codex | 1.5h |
| `MCPF-P6-09` | Shared | Revisar diffs e criar commits separados da fase | Você + Codex | 0.25h |

**Testing**:

- Protocol conformance: transporte, descoberta, tools e erros.
- Security: origem inválida, token revogado, escopo incorreto, cross-account, replay e rate limit.
- Performance: P95 de leitura ≤ 3s e escrita unitária ≤ 5s.
- Audit completeness: 100% das invocações possuem journal correspondente.
- Resilience: leases expirados são retomados e estados `Unknown` nunca provocam retry automático.
- Frontend behavior: estados operacionais, filtros, acessibilidade e responsividade.
- Regression: `dotnet test`, `npm test`, `npm run lint` e `npm run build`.

**Acceptance Criteria**:

- [ ] Nenhum teste de cross-account retorna dados.
- [ ] Nenhuma escrita ocorre sem confirmação válida.
- [ ] 100% das operações testadas possuem auditoria.
- [ ] O reconciliador não possui operação vencida sem classificação ou alerta.
- [ ] Os limites de latência do PRD são atendidos no ambiente de teste.
- [ ] O frontend funciona em mobile, desktop, modo claro e escuro.
- [ ] As ferramentas não dependem de comportamento exclusivo de fornecedor.
- [ ] Todas as suítes dos dois repositórios passam.

**Dependencies**: Fases 1 a 5 concluídas. Clientes de teste MCP disponíveis.

**Commit checkpoint**:

- Backend: `refactor(mcp): harden security and observability`
- Frontend: `refactor(mcp): harden connection experience`

---

### Phase 7 — Homologação completa e prontidão para piloto

**Target date**: 2026-07-31

**Goal**: Publicar backend e frontend em homologação, executar o aceite integral e deixar o produto pronto para iniciar testes com usuários.

**Vertical slice**: Imagens versionadas → configuração de homologação → deploy backend/frontend → conexão MCP externa → dez jornadas do PRD → monitoramento → rollback testado → checklist de piloto.

**Requirements covered**: Todos os requisitos `MCP-01` a `MCP-125`.

**Tasks**:

| ID | Workstream | Task | Owner | Estimate |
| --- | --- | --- | --- | --- |
| `MCPF-P7-01` | Backend | Preparar build Docker imutável, variáveis MCP, segredos, TLS, índices do MongoDB standalone e health dos journals/reconciliador | Você + Codex | 1h |
| `MCPF-P7-02` | Frontend | Preparar build imutável com URL de homologação, flags, CSP e configuração sem valores hardcoded de produção | Você + Codex | 1h |
| `MCPF-P7-03` | Shared | Publicar backend e frontend em homologação sem alterar o fluxo de produção atual | Você + Codex | 1.5h |
| `MCPF-P7-04` | Shared | Executar smoke ponta a ponta das dez jornadas de consulta, análise, CRUD, exclusão e importação | Você + Codex | 1.5h |
| `MCPF-P7-05` | Shared | Executar gates finais de reconciliação, 1.000 itens, latência, autorização, auditoria e conformidade | Você + Codex | 1h |
| `MCPF-P7-06` | Shared | Configurar painéis/alertas mínimos e validar correlação entre chamada, confirmação, registro e auditoria | Você + Codex | 1h |
| `MCPF-P7-07` | Shared | Executar ensaio de rollback das imagens e das feature flags, preservando auditoria e registros válidos | Você + Codex | 0.75h |
| `MCPF-P7-08` | Shared | Criar checklist de piloto, guia de suporte, limitações conhecidas e evidências de aceite | Você + Codex | 1h |
| `MCPF-P7-09` | Shared | Revisar diffs finais, criar commits da fase e registrar os SHAs implantados em homologação | Você + Codex | 0.25h |

**Testing**:

- Deployment smoke: health, frontend, conexão, autorização e descoberta MCP.
- End-to-end: 10 cenários representativos do PRD.
- Reconciliation: totais antes/depois e registros criados/alterados/excluídos.
- Load: importação de 1.000 itens e volume concorrente controlado.
- Security: conta A versus conta B, token revogado e origem inválida.
- Rollback: imagem anterior e flags desabilitadas.
- Observability: métricas, logs redigidos e auditoria consultável.
- Resilience: operação interrompida em homologação é retomada/classificada sem duplicar efeito.

**Acceptance Criteria**:

- [ ] Backend e frontend estão acessíveis em homologação por HTTPS.
- [ ] Um cliente MCP externo compatível conecta-se ao ambiente.
- [ ] Todos os 125 requisitos possuem evidência ou teste associado.
- [ ] Todas as suítes e gates finais passam.
- [ ] Nenhum documento original foi recebido ou armazenado.
- [ ] Nenhuma ferramenta depende de transação multi-documento ou replica set.
- [ ] Rollback foi ensaiado com sucesso.
- [ ] SHAs, configuração e evidências da homologação estão registrados.
- [ ] O ambiente está pronto para iniciar o piloto, sem ter realizado teste com usuários antes desta fase.

**Dependencies**: Fase 6 concluída. Acesso a homologação, DNS/TLS, MongoDB, secrets e processo de deploy disponíveis.

**Commit checkpoint**:

- Backend: `chore(mcp): prepare staging pilot`
- Frontend: `chore(mcp): prepare staging pilot`

## Daily Schedule

| Date | Phase | Deliverable | Hard Gate at End of Day |
| --- | --- | --- | --- |
| 25/07 | Phase 1 | Conexão + leitura de categorias + auditoria + UI mínima | Cliente MCP conecta e consulta somente a própria conta |
| 26/07 | Phase 2 | Leituras e análises completas | Reconciliação de consultas em 100% das fixtures |
| 27/07 | Phase 3 | Motor de confirmação + categorias/receitas | Nenhuma escrita sem confirmação; idempotência comprovada |
| 28/07 | Phase 4 | CRUD dos cinco domínios | Matriz completa de escrita passa |
| 29/07 | Phase 5 | Importação de 1.000 itens | Sucesso parcial, duplicidade e correção passam |
| 30/07 | Phase 6 | Hardening e UX final | Segurança, desempenho, conformidade e builds passam |
| 31/07 | Phase 7 | Homologação pronta | E2E, observabilidade e rollback aprovados |

## Milestones

| Milestone | Target Date | Description |
| --- | --- | --- |
| M1 — Protocolo provado | 2026-07-25 | Cliente remoto autenticado consulta categorias e gera auditoria |
| M2 — Leitura completa | 2026-07-26 | Cinco domínios e análises disponíveis sem confirmação |
| M3 — Escrita completa | 2026-07-28 | CRUD confirmado dos cinco domínios concluído |
| M4 — Importação completa | 2026-07-29 | Lote de 1.000 itens com sucesso parcial e correção validado |
| M5 — Release candidate | 2026-07-30 | Segurança, observabilidade, UX, testes e builds aprovados |
| M6 — Homologação pronta | 2026-07-31 | Solução publicada, monitorada, testada e pronta para piloto |

## Dependencies

| Dependency | Type | Owner | Status | Needed By | Risk if Delayed |
| --- | --- | --- | --- | --- | --- |
| Decisão de transporte, SDK e autorização MCP | Technical | Você + Codex | Resolved | 25/07 | Bloqueia a Fase 1 |
| Versão estável do SDK C# oficial validada com .NET 9 | External/Technical | Codex | Resolved | 25/07 | Pode gerar incompatibilidade ou retrabalho |
| MongoDB standalone local, homologação e produção | Internal | Você | Confirmed constraint | 25/07 e 30/07 | Bloqueia persistência, integração e deploy |
| Índices únicos, journal e reconciliador validados | Technical | Você + Codex | Open | 27/07 | Bloqueia ferramentas de escrita |
| URL HTTPS e configuração de origem para MCP | Internal | Você | Open | 30/07 | Clientes remotos não conectam |
| Credenciais e acesso ao deploy de homologação | Internal | Você | Open | 30/07 | Bloqueia a Fase 7 |
| Processo de deploy do frontend em homologação | Internal | Você + Codex | Open | 30/07 | Frontend não fica disponível para piloto |
| Cliente oficial de inspeção e segundo cliente MCP compatível | External | Codex | Open | 25/07 e 30/07 | Reduz evidência de interoperabilidade |
| Política definitiva de retenção de auditoria | Product/Privacy | Você | Open | Antes de produção | Não bloqueia homologação; bloqueia produção |
| Aviso/consentimento sobre processamento externo | Product/Privacy | Você | Open | 30/07 | Bloqueia aceite de privacidade |

## Risks

| Risk | Impact | Probability | Mitigation |
| --- | --- | --- | --- |
| Prazo de sete dias para 125 requisitos | H | H | Gate diário, escopo congelado, automação de testes, commits por fase e uso da contingência apenas para bloqueadores críticos |
| Autorização MCP incompatível com clientes genéricos | H | M | Seguir especificação HTTP MCP, validar com dois clientes e evitar fluxo proprietário |
| SDK C# sofrer mudança durante a semana | H | M | Fixar versão estável no primeiro dia e não atualizar durante a implementação |
| Escrita executada sem confirmação válida | H | M | Motor único de prévia/confirmar, escrita desabilitada por flag até Fase 3 e testes negativos obrigatórios |
| Vazamento entre contas ou uso de contexto compartilhado | H | M | Derivar usuário da credencial, ignorar/rejeitar contexto externo e testar conta A versus conta B em todas as fases |
| Duplicidade após timeout ou reenvio | H | M | Chaves idempotentes, status persistido e ferramenta de verificação de resultado |
| Falha entre journal e mutação financeira | H | H | Journal antes do efeito, lease, effect markers, passos idempotentes, fault injection e reconciliação |
| Exclusão concluída com causalidade inconclusiva | H | M | Não repetir quando o alvo estiver ausente; retornar `Unknown` e permitir verificação segura |
| Operação multi-documento permanecer parcial | H | M | Passos persistidos, retomada dos pendentes e alertas para operação travada |
| Importação de 1.000 itens exceder recursos | M | M | Limite explícito, índices, validação item a item, teste de carga e resposta resumida |
| Logs/auditoria armazenarem dados excessivos | H | M | Redação centralizada, allowlist de campos auditáveis e teste automatizado de ausência de segredos/documentos |
| Frontend atual possuir débito de sessão/localStorage | M | M | Isolar a configuração MCP, não reutilizar credenciais MCP no storage comum e registrar evolução de sessão fora deste escopo |
| Homologação indisponível ou sem TLS/DNS | H | M | Verificar acesso até 30/07, preparar deploy reproduzível e ativar contingência somente para infraestrutura |
| Alteração local preexistente entrar em commit | M | M | Excluir explicitamente `launchSettings.json` do stage e revisar `git diff --cached` em todo checkpoint |

## Requirements Coverage

| PRD Range | Capability | Primary Phase | Verification |
| --- | --- | --- | --- |
| `MCP-01`–`MCP-16` | Conexão, onboarding, autenticação e privacidade | 1, 2 e 6 | Contrato, integração, UI, segurança |
| `MCP-17`–`MCP-33` | Consultas e análises | 1 e 2 | Reconciliação, paginação, estado vazio |
| `MCP-34`–`MCP-49` | Prévia, confirmação e idempotência | 3 | Unidade, integração, concorrência e replay |
| `MCP-50`–`MCP-71` | CRUD dos cinco domínios | 3 e 4 | Matriz de domínio e regressão |
| `MCP-72`–`MCP-97` | Importação estruturada | 5 | Lote 1.000, parcial, correção, privacidade |
| `MCP-98`–`MCP-114` | Auditoria e histórico | 1, 3, 5 e 6 | Completude, isolamento e UI |
| `MCP-115`–`MCP-125` | Falhas, limites e previsibilidade | 2, 3, 4 e 6 | Erros, limites, resultado desconhecido e incidentes |
| Todos | Homologação e prontidão | 7 | E2E, performance, segurança e rollback |

## Testing Strategy

| Test Type | Scope | Approach | Critical Scenarios |
| --- | --- | --- | --- |
| Backend unit | Serviços MCP, validadores, estados, leases e mapeadores | xUnit com dependências controladas | Expiração, uso único, idempotência, reconciliação, ambiguidade, duplicidade, redação |
| Backend integration | HTTP MCP, APIs de configuração, serviços e MongoDB standalone | Host de teste com banco isolado por execução | Descoberta, auth, conta A/B, CRUD, journal, auditoria, parcial |
| Protocol contract | Tools, schemas, erros e transporte | Snapshots/schema + inspector/conformance runner | Compatibilidade, leitura versus escrita, erros JSON-RPC |
| Frontend unit/component | Serviços, componentes e estados do modal | Runner Vue/TypeScript integrado ao `npm test` | Criar/revogar, segredo, estados vazios, histórico, filtros |
| End-to-end | Browser + frontend + backend + cliente MCP | Ambiente local e homologação com fixtures conhecidas | Dez jornadas do PRD |
| Security | Autenticação, autorização, origem, replay e logs | Casos negativos automatizados e revisão dirigida | Cross-account, token revogado, confirmação roubada, segredo em log |
| Reconciliation | Totais e mudanças financeiras | Dataset determinístico antes/depois | Totais, comparação, criação, alteração e exclusão |
| Fault injection | Janelas entre journal, reserva, efeito e finalização | Interrupções determinísticas por estágio | Sem efeito sem journal, retomada segura, `Unknown` honesto e zero duplicidade |
| Load/performance | Consulta, escrita e lote | Runner repetível com métricas P50/P95/P99 | Leitura ≤3s P95, escrita ≤5s P95, 1.000 itens |
| Regression | Codebases existentes | `dotnet test`, `npm test`, `npm run lint`, `npm run build`, builds Docker | Domínios tradicionais, dashboard, sessão e UI |
| Smoke deployment | Homologação | Checklist executável após deploy | Health, TLS, CORS/origin, conexão, tool call, histórico |

### Test Data Management

- Criar usuários isolados `mcp-owner-a`, `mcp-owner-b` e `mcp-empty`.
- Usar dados sintéticos; não copiar informações financeiras reais.
- Criar fixture reconciliável para três períodos, cinco domínios e múltiplas categorias.
- Criar payloads estruturados que representem Excel e CSV sem armazenar arquivos.
- Gerar lote determinístico de 1.000 itens com válidos, inválidos, pendentes e duplicidades.
- Limpar somente dados identificados pelo namespace da execução de teste.
- Nunca registrar tokens, segredos, arquivo, linha completa ou payload integral.

## Observability and Operational Gates

### Minimum Metrics

- `mcp_connections_active`
- `mcp_tool_calls_total{tool,status}`
- `mcp_tool_duration_ms{tool}`
- `mcp_auth_denied_total{reason}`
- `mcp_confirmation_total{status}`
- `mcp_confirmation_replay_total`
- `mcp_import_items_total{status,type}`
- `mcp_journal_write_failure_total`
- `mcp_operation_reconciliation_total{outcome}`
- `mcp_operation_stuck_total`
- `mcp_unknown_operation_total`
- `mcp_rate_limited_total`

### Structured Log Fields

- `correlationId`
- `userIdHash`
- `connectionId`
- `toolName`
- `operationClass`
- `previewId`
- `operationId`
- `importId`
- `leaseAttempt`
- `status`
- `durationMs`
- `errorCode`

### Never Log

- Credenciais ou tokens.
- Conteúdo de Excel/CSV.
- Payload financeiro integral.
- Dados de outra conta.
- Segredo de confirmação.

## Rollback Plan

### Deployment Strategy

1. Construir imagens imutáveis identificadas pelo SHA de cada repositório.
2. Publicar primeiro no ambiente de homologação.
3. Manter `MCP_FEATURE_ENABLED=false` durante a subida inicial.
4. Validar health, banco, logs e frontend.
5. Habilitar ferramentas de leitura internamente.
6. Executar smoke de leitura e auditoria.
7. Habilitar ferramentas de escrita internamente.
8. Executar confirmação, idempotência e importação.
9. Liberar o ambiente somente após todos os gates da Fase 7.

### Rollback Triggers

- Qualquer acesso cross-account.
- Qualquer escrita sem confirmação válida.
- Qualquer duplicação causada por replay.
- Qualquer falha de auditoria confirmada.
- Qualquer efeito financeiro sem journal anterior.
- Reconciliador indisponível com operações vencidas por mais de 2 minutos.
- Erro das ferramentas acima de 1% em smoke controlado.
- Divergência financeira em qualquer cenário de reconciliação.
- P95 acima de 6 segundos para leitura ou 10 segundos para escrita durante o gate final.
- Segredo, documento ou payload integral encontrado em log.
- Health check instável após deploy.

### Immediate Rollback Steps

1. Definir `MCP_WRITE_TOOLS_ENABLED=false`.
2. Se o incidente não estiver limitado à escrita, definir `MCP_FEATURE_ENABLED=false`.
3. Revogar conexões de homologação afetadas.
4. Reimplantar as imagens anteriores por SHA.
5. Manter coleções de auditoria e resultados para investigação.
6. Bloquear novos rascunhos/importações.
7. Não remover nem reiniciar manualmente journals em `Executing`, `Reconciling` ou `Unknown`.
8. Confirmar que endpoints tradicionais do FinanMap permanecem saudáveis.
9. Registrar horário, SHA, correlação e impacto.

### Database Rollback

- Alterações de banco devem ser aditivas durante a V1.
- Índices novos podem ser removidos somente após confirmar que não são usados pela versão anterior.
- Auditoria, prévias e importações não devem ser apagadas automaticamente no rollback.
- Journals e effect markers devem permanecer disponíveis para reconciliação da versão compatível.
- Registros financeiros válidos já confirmados não devem ser revertidos em massa.
- Não executar compensação automática destrutiva para passos já concluídos.
- Correção de dados exige análise por operação auditada e autorização explícita.

### Frontend Rollback

- Reimplantar a imagem anterior por SHA.
- Ocultar a seção MCP por feature flag quando o backend estiver desabilitado.
- Invalidar cache do ambiente somente após confirmar o artefato correto.
- Manter uma mensagem de indisponibilidade sem expor detalhes internos.

### Post-Rollback

- Produzir análise de causa raiz.
- Criar teste que reproduza a falha.
- Corrigir na branch da feature.
- Reexecutar toda a fase afetada e os gates da Fase 7.
- Publicar novamente somente com evidência de correção.

## Definition of Done

- [ ] Backend e frontend estão na branch `feature/mcp-financeiro-conversacional`.
- [ ] Todos os requisitos `MCP-01` a `MCP-125` possuem tarefa e evidência.
- [ ] Todas as ferramentas de leitura são executáveis sem confirmação.
- [ ] Todas as ferramentas de escrita exigem confirmação específica de uso único.
- [ ] CRUD completo dos cinco domínios funciona via MCP.
- [ ] Importação de 1.000 itens atende sucesso parcial, correção e idempotência.
- [ ] Nenhum arquivo ou documento é recebido/armazenado.
- [ ] Conta A não acessa nenhum dado da conta B.
- [ ] Auditoria está completa e visível ao titular.
- [ ] Toda escrita possui journal anterior ao efeito e chave idempotente.
- [ ] Fault injection comprova retomada segura ou estado `Unknown` sem duplicidade.
- [ ] Nenhuma implementação depende de transação multi-documento ou replica set.
- [ ] UI de conexão, dicas, prompts e histórico está responsiva e acessível.
- [ ] Suíte de conformidade MCP passa.
- [ ] `dotnet test` passa.
- [ ] `npm test` passa.
- [ ] `npm run lint` passa.
- [ ] `npm run build` passa.
- [ ] Imagens Docker constroem com sucesso.
- [ ] Homologação está acessível por HTTPS.
- [ ] Métricas e alertas mínimos estão ativos.
- [ ] Rollback foi ensaiado.
- [ ] SHAs implantados e evidências foram registrados.
- [ ] Nenhum teste com usuário ocorreu antes da conclusão integral.

## Validation Checklist

- [x] Technical design referenced if one exists
- [x] Phase 1 is a tracer bullet delivering end-to-end value
- [x] Every phase has goal, tasks, testing, acceptance criteria, and dependencies
- [x] All task IDs follow the `MCPF-PN-NN` convention and are unique
- [x] Testing is embedded in each phase
- [x] No phase is purely setup or infrastructure
- [x] Milestones have target dates
- [x] Dependencies have owners, status and delay risk
- [x] Risks include impact, probability and mitigation
- [x] Testing Strategy is present
- [x] Rollback Plan is present
- [x] Backend and frontend responsibilities are separated
- [x] Requirements `MCP-01` through `MCP-125` are mapped
- [x] Homologation readiness is the final delivery
- [x] User testing begins only after full implementation

## Technical References

- MCP specification, Streamable HTTP transport: <https://modelcontextprotocol.io/specification/2025-11-25/basic/transports>
- MCP authorization tutorial: <https://modelcontextprotocol.io/docs/tutorials/security/authorization>
- Official MCP C# SDK: <https://github.com/modelcontextprotocol/csharp-sdk>

