# Review — Lista de Compras Planejadas (Back-end)

| Status       | Aprovado com ressalvas |
|--------------|------------------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Escopo revisado:** Fase 01 — tracer bullet de cadastro e consulta
**Versão da avaliação:** 2
**Snapshot revisado:** `da6ca4d`

## Artefatos analisados

- PRD: `.specs/lista-compras-planejadas/PRODUCT-REQUIREMENTS.md` (Aprovado).
- Plano: `.specs/lista-compras-planejadas/IMPLEMENTATION-PLAN.md` (Aprovado).
- Fase: `.specs/lista-compras-planejadas/fases/fase-01-tracer-bullet-cadastro-consulta.md`.
- Estado: `.specs/lista-compras-planejadas/fases/IMPLEMENTATION-STATE.md`.
- Código da feature nas camadas Domain, Application, Infra.data e WebApi.
- Reavaliação independente somente leitura: achados A-01/A-02 da versão anterior foram confrontados com o HEAD atual.

## Verificações

| Verificação | Resultado | Evidência |
|-------------|-----------|-----------|
| Requisitos e critérios da Fase 01 | Atendidos com ressalva | Matriz abaixo; persistência real não foi exercitada. |
| Testes | Atendida | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 47 aprovados. |
| Testes da aplicação | Atendida | `CompraPlanejadaServiceTests` — 15 aprovados; inclui nulo em links, vazio, isolamento, ordenação, CRUD e falhas sem mutação. |
| Testes do domínio | Atendida | `CompraPlanejadaDomainTests` — 9 aprovados. |
| Build | Atendida | `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — 0 erros; warnings preexistentes registrados no ambiente. |
| Formato | Atendida com ressalva | Format-check limitado à feature passou; o global acusa whitespace preexistente fora do escopo. |
| Complexidade | Atendida | Inspeção manual: funções da feature permanecem abaixo do limite 10; consulta e soma são O(N), sem I/O por item. |
| Design e escopo | Atendida | Camadas, `Result`, `IUsuarioLogado` e mapping automático existentes foram preservados; design técnico dispensado pelo plano. |

## Matriz de rastreabilidade

| Requisito | Evidência objetiva | Status |
|-----------|--------------------|--------|
| LCP-BE-01 — cadastro | Entidade, serviço e endpoints protegidos; `AdicionarEListar_MantemLinksETotalEstimado` | Comprovado por código/teste; Mongo real pendente |
| LCP-BE-04 — múltiplos links | DTO, value object, mapping e teste com dois links | Comprovado por código/teste; BSON real pendente |
| LCP-BE-05 — total estimado | Soma decimal e `ListarPendentes_SemItensRetornaColecaoVaziaETotalZero` | Comprovado |
| LCP-BE-06 — prioridade/data | Sort do repositório e `ListarPendentes_PreservaOrdemDePrioridadeERecencia` | Comprovado por código/teste; Mongo real pendente |
| EXPECT-BE-01 — precisão monetária | `decimal`, testes de estimativa e soma `0.30m` | Comprovado por código/teste |
| EXPECT-BE-03 — centenas/índice | Índice composto por contexto, estado, prioridade e data; materialização linear | Com ressalva; índice real e massa não medidos |
| EXPECT-BE-04 — erros distinguíveis | Validation para dados inválidos, Forbidden para visualizador, teste de link nulo | Comprovado |
| EXPECT-BE-05 — sem confirmação parcial | Construção/validação ocorre antes de `Add`; testes mantêm fake vazio ou item inalterado | Comprovado para falhas exercitadas |

## Achados

| ID | Severidade | Achado | Encaminhamento |
|----|-----------|--------|----------------|
| A-01 | Baixo, operacional | Smoke autenticado, persistência/serialização BSON e inspeção real dos índices Mongo não foram executados porque Docker não conecta e não há Mongo local disponível. | Reexecutar em ambiente com Mongo antes da publicação. |
| A-02 | Processo | O branch contém commits da Fase 02 antes da conclusão deste gate retrospectivo; portanto, a ordem formal F01 → review → F02 não foi respeitada neste ciclo. | Manter a ressalva no histórico e não iniciar a Fase 03 sem review aprovado da Fase 02. |

## Correções verificadas desde a versão 1

- `CompraPlanejadaService` trata item nulo em `linksLojas` como `Validation`, antes de persistir.
- A regressão automatizada cobre consulta vazia, total zero, isolamento entre proprietários e ordenação por prioridade/recência.
- A suíte atual passou com 47 testes.

## Limitações e riscos residuais

- Docker falha por daemon indisponível (`dockerDesktopLinuxEngine`); `localhost:27017` também não está acessível.
- O format-check global acusa somente arquivos preexistentes fora da feature; o format-check limitado passou.
- A ausência de paginação continua risco residual para volumes além de centenas de itens e está prevista na Fase 04.

## Veredito

**Aprovado com ressalvas.**

Os achados funcionais apontados na revisão anterior foram corrigidos e possuem cobertura objetiva. A aprovação é retrospectiva quanto à ordem de fases e condicionada ao smoke Mongo/HTTP em ambiente disponível antes da publicação. A Fase 02 exige review próprio antes de qualquer avanço para a Fase 03.

## Histórico de revisões anteriores

- **Versão 1 — 2026-09-09:** `Aprovado com ressalvas` por avaliação local; posteriormente reprovada pela revisão independente por validação ausente para `linksLojas: [null]` e cobertura insuficiente de vazio/isolamento/ordenação.
- **Versão 2 — 2026-09-09:** achados funcionais encerrados no snapshot `da6ca4d`; mantidas ressalvas operacional e processual.
