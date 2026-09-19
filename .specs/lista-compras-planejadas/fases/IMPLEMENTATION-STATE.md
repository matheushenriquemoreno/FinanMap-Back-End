# Estado da Implementação — Lista de Compras Planejadas (Back-end)

| Status       | Concluída  |
| ------------ | ---------- |
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

## Fase ativa

Gate 5 aprovado — Fases 01 a 04 implementadas, revisadas e concluídas no escopo local.

Cada fase deve ser executada isoladamente e aprovada por `review` antes da ativação da fase seguinte.

## Fases

| #   | Fase                                      | Arquivo                                          | Status    | Concluída em |
| --- | ----------------------------------------- | ------------------------------------------------ | --------- | ------------ |
| 01  | Tracer bullet de cadastro e consulta      | fases/fase-01-tracer-bullet-cadastro-consulta.md | Concluída | 2026-09-09   |
| 02  | Gestão dos itens pendentes                | fases/fase-02-gestao-itens-pendentes.md          | Concluída | 2026-09-09   |
| 03  | Ciclo da compra e integração com despesas | fases/fase-03-ciclo-compra-despesas.md           | Concluída | 2026-09-09   |
| 04  | Compartilhamento, desempenho e robustez   | fases/fase-04-compartilhamento-robustez.md       | Concluída | 2026-09-09   |

## Tarefas

| ID  | Fase | Status    | Evidências                                                                                                                                                              |
| --- | ---- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| T01 | 01   | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaDomainTests` — 9 aprovados; entidade, prioridade e links validados.                                         |
| T02 | 01   | Concluída | `dotnet build ... --no-restore` — solução aprovada; mapping/index composto adicionado. Healthcheck Mongo integrado executado em Docker.                                 |
| T03 | 01   | Concluída | `dotnet build ... --no-restore` — repositório com filtro de contexto/estado e ordenação aprovado. Persistência Mongo integrada verificada em Docker.                    |
| T04 | 01   | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — 15 aprovados; DTO, contexto, links, total decimal, vazio, isolamento e ordenação validados. |
| T05 | 01   | Concluída | `dotnet build ... --no-restore` — endpoints `POST`/`GET /api/compras-planejadas` publicados, protegidos e incluídos no OpenAPI; smoke HTTP/Mongo integrado executado.   |
| T06 | 02   | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — atualização e validações aprovadas.                                                         |
| T07 | 02   | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — exclusão contextual, repetição e total aprovados.                                           |
| T08 | 02   | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — validações de campos/links e falhas sem mutação aprovadas.                                  |
| T09 | 02   | Concluída | `dotnet test ...` — 66 aprovados; regressão completa consolidada após as fases de ciclo e autorização.                                                                  |
| T10 | 03   | Concluída | `CompraPlanejadaLifecycleTests`: conclusão válida, valor/data inválidos e repetição; `dotnet test` — 66 aprovados.                                                      |
| T11 | 03   | Concluída | Consulta exclusiva de comprados, estimativa/real/data e separação pendente/comprado cobertas por testes de serviço.                                                     |
| T12 | 03   | Concluída | Gateway sobre `IDespesaService`, vínculo único, valor real, categoria/período, falha de criação e compensação cobertos; leitura do acumulado ocorre antes da inserção.  |
| T13 | 03   | Concluída | Reversão sem despesa, limpeza dos dados reais, retorno à ordenação e total cobertos.                                                                                    |
| T14 | 03   | Concluída | Preservação, exclusão explícita, falha de exclusão e restauração do estado cobertos por testes.                                                                         |
| T15 | 03   | Concluída | Exclusão de comprado preserva o identificador da despesa e não chama sua exclusão.                                                                                      |
| T16 | 03   | Concluída | Totais `decimal` estimado/real e massa de 400 itens cobertos sem perda de precisão.                                                                                     |
| T17 | 04   | Concluída | Proprietário, editor compartilhado e visualizador; leituras contextuais e mutações protegidas por `PodeEditar`/`IdContextoDados`.                                       |
| T18 | 04   | Concluída | Índice composto, massa de 400 itens, ordenação, agregados, build e format-check da feature verificados.                                                                 |

## Bloqueios e desvios

- Fase 01: smoke autenticado, healthcheck, CORS e persistência Mongo foram executados em Docker; inspeção dedicada de índices permanece pendente.
- Fase 01: `dotnet format` global acusa whitespace preexistente fora da feature; o format-check limitado aos arquivos da feature passou.
- Fases 03–04: os fluxos autenticados de conclusão, reversão, exclusão e vínculo com despesa foram executados; cenários formais de falha transacional, inspeção de plano e métricas de latência/memória permanecem como ressalvas pré-publicação.
