# Estado da Implementação — Lista de Compras Planejadas (Back-end)

| Status       | Em execução |
|--------------|------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

## Fase ativa

Fase 01 — Tracer bullet de cadastro e consulta.

Cada fase deve ser executada isoladamente e aprovada por `review` antes da ativação da fase seguinte.

## Fases

| #  | Fase | Arquivo | Status | Concluída em |
|----|------|---------|--------|--------------|
| 01 | Tracer bullet de cadastro e consulta | fases/fase-01-tracer-bullet-cadastro-consulta.md | Concluída | 2026-09-09 |
| 02 | Gestão dos itens pendentes | fases/fase-02-gestao-itens-pendentes.md | Em execução | — |
| 03 | Ciclo da compra e integração com despesas | fases/fase-03-ciclo-compra-despesas.md | Pendente | — |
| 04 | Compartilhamento, desempenho e robustez | fases/fase-04-compartilhamento-robustez.md | Pendente | — |

## Tarefas

| ID  | Fase | Status | Evidências |
|-----|------|--------|------------|
| T01 | 01 | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaDomainTests` — 9 aprovados; entidade, prioridade e links validados. |
| T02 | 01 | Concluída | `dotnet build ... --no-restore` — solução aprovada; mapping/index composto adicionado. Smoke Mongo pendente de ambiente. |
| T03 | 01 | Concluída | `dotnet build ... --no-restore` — repositório com filtro de contexto/estado e ordenação aprovado. Smoke Mongo pendente de ambiente. |
| T04 | 01 | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — 4 aprovados; DTO, contexto, links e total decimal validados. |
| T05 | 01 | Concluída | `dotnet build ... --no-restore` — endpoints `POST`/`GET /api/compras-planejadas` publicados, protegidos e incluídos no OpenAPI; smoke Mongo pendente. |
| T06 | 02 | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — atualização e validações aprovadas. |
| T07 | 02 | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — exclusão contextual, repetição e total aprovados. |
| T08 | 02 | Pendente | — |
| T09 | 02 | Pendente | — |
| T10 | 03 | Pendente | — |
| T11 | 03 | Pendente | — |
| T12 | 03 | Pendente | — |
| T13 | 03 | Pendente | — |
| T14 | 03 | Pendente | — |
| T15 | 03 | Pendente | — |
| T16 | 03 | Pendente | — |
| T17 | 04 | Pendente | — |
| T18 | 04 | Pendente | — |

## Bloqueios e desvios

- Fase 01: smoke autenticado e inspeção real de índices Mongo não executados porque o daemon Docker local não está disponível.
- Fase 01: `dotnet format` global acusa whitespace preexistente fora da feature; o format-check limitado aos arquivos da feature passou.
