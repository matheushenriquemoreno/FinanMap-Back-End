# Review — Lista de Compras Planejadas (Back-end)

| Status       | Aprovado com ressalvas |
|--------------|------------------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Escopo revisado:** Fase 02 — gestão dos itens pendentes
**Versão da avaliação:** 3
**Snapshot revisado:** `0c96e95`

## Artefatos analisados

- PRD e plano aprovados em `.specs/lista-compras-planejadas/`.
- Fase: `fases/fase-02-gestao-itens-pendentes.md`.
- Estado: `fases/IMPLEMENTATION-STATE.md`.
- Código Domain, Application, Infra.data e WebApi da compra planejada.
- Reavaliação independente somente leitura do escopo T06–T09 após as correções.

## Verificações

| Verificação | Resultado | Evidência |
|-------------|-----------|-----------|
| T06/T07/T08 e critérios da Fase 02 | Atendidos com ressalva | Edição, exclusão, isolamento, validações e estados cobertos por código/teste. |
| Regressão completa | Atendida | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 50 aprovados. |
| Build | Atendida | `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — 0 erros; warnings preexistentes no restante da solução. |
| Formato | Atendida com ressalva | Format-check limitado à feature passou; global falha em whitespace preexistente fora do escopo. |
| Complexidade e algoritmo | Atendida | Atualização/exclusão são operações lineares no serviço; persistência usa filtro contextual e o novo replace confirma `MatchedCount`. |
| Escopo | Atendida | Não foram incluídas transições de compra, reservadas à Fase 03. |

## Matriz de rastreabilidade

| Requisito | Evidência objetiva | Status |
|-----------|--------------------|--------|
| LCP-BE-02 — alterar pendente | `Atualizar`, PUT contextual, teste de todos os campos, descrição e substituição de links | Comprovado |
| LCP-BE-03 — excluir pendente | `Excluir`, DELETE contextual, repetição, outro proprietário e total subsequente | Comprovado |
| LCP-BE-04 — links | Construção de links em criação/atualização e regressões válidas/inválidas | Comprovado |
| EXPECT-BE-01 — precisão | `decimal` preservado nos DTOs, entidade e soma | Comprovado |
| EXPECT-BE-04 — falhas distinguíveis | Validation, NotFound e Forbidden cobertos por testes | Comprovado |
| EXPECT-BE-05 — sem mutação parcial | Validação antes da atribuição, edição de comprado bloqueada e replace contextual confirmado | Comprovado nos cenários testados |

## Achados

| ID | Severidade | Achado | Encaminhamento |
|----|-----------|--------|----------------|
| A-01 | Baixo, operacional | Smoke HTTP autenticado, persistência BSON e inspeção de índices não executados por ausência de Mongo/Docker. | Reexecutar antes da publicação. |
| A-02 | Processo | A Fase 02 foi implementada antes do primeiro gate independente da Fase 01; o desvio está registrado na revisão v2. | Não iniciar Fase 03 sem este gate e sem preservar o histórico. |

## Correções verificadas desde a revisão da Fase 02

- Atualização de comprado retorna `Validation` e não altera campos.
- Descrição e substituição de links são verificadas explicitamente.
- PUT e DELETE em contexto somente visualização retornam `Forbidden`.
- Repositório usa `AtualizarSePendente` com filtro por ID, proprietário e estado e só confirma quando `MatchedCount == 1`.
- A documentação foi corrigida para 50 testes aprovados.

## Limitações e riscos residuais

- Docker falha por daemon indisponível (`dockerDesktopLinuxEngine`) e `localhost:27017` não está acessível.
- O format-check global permanece limitado por arquivos preexistentes fora da feature.
- Não há evidência de execução HTTP/Mongo neste ambiente; os testes de feature são unitários com fake de repositório.

## Veredito

**Aprovado com ressalvas.**

O comportamento da Fase 02 atende ao recorte T06–T09, incluindo proteção de item comprado e confirmação contextual de atualização. A ressalva operacional do Mongo e o desvio histórico de ordem permanecem explícitos. Os requisitos LCP-BE-07–LCP-BE-14 e LCP-BE-17 não foram cobrados neste gate porque pertencem à Fase 03 conforme o plano.

## Histórico de revisões anteriores

- **Versão 1 — 2026-09-09:** revisão local da Fase 01, aprovada com ressalvas.
- **Versão 2 — 2026-09-09:** reavaliação independente da Fase 01, aprovando após correção de nulo/cobertura e registrando desvio de sequência.
- **Versão 3 — 2026-09-09:** review independente da Fase 02 no escopo correto, aprovado com ressalvas após correção de `MatchedCount`; 50 testes aprovados.
