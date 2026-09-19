# Plano de Implementação — Lista de Compras Planejadas (Back-end)

| Status       | Aprovado   |
| ------------ | ---------- |
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

PRD de referência: `.specs/lista-compras-planejadas/PRODUCT-REQUIREMENTS.md` (Aprovado)

Design técnico: dispensado. Premissas no lugar: a feature seguirá as camadas já existentes `Domain` → `Application` → `Infra.data` → `WebApi`, o padrão de Minimal APIs, `Result`, repositórios MongoDB, injeção de dependência e `IUsuarioLogado`; a integração com despesas reutilizará o domínio e o serviço existentes. Qualquer necessidade de alterar essa arquitetura interrompe o plano e retorna para `create-technical-design`.

## Histórico de atualizações

| Data       | Alteração                                                                                                                                |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-09-09 | Versão inicial criada a partir do PRD aprovado, incluindo a preservação da despesa ao excluir um item comprado.                          |
| 2026-09-09 | Gate 3 aprovado pelo solicitante; plano liberado para execução futura pela skill `implement`.                                            |
| 2026-09-09 | Implementação das quatro fases concluída; review final independente e smoke autenticado integrado executados com ressalvas operacionais. |
| 2026-09-09 | Gate 5 aprovado com ressalvas operacionais; as quatro fases e 18 tarefas foram concluídas no escopo local.                               |

## Objetivo geral da implementação

Entregar o ciclo completo de compras planejadas no back-end: cadastro e gestão de pendentes, conclusão e histórico de compras, vínculo opcional com despesa, reversão, totais e autorização no contexto compartilhado.

## Estratégia de execução

O trabalho começa com um tracer bullet de criação e consulta, provando domínio, persistência, aplicação e endpoint. Em seguida entram as mutações dos pendentes, o ciclo de compra com a integração de despesas e, por fim, autorização, desempenho e robustez. Cada fase inclui seus próprios testes e deve passar por `review` antes da próxima.

O contrato do back-end antecede a fase equivalente do front-end. Mudanças incompatíveis no formato das operações devem ser registradas nos dois planos antes da implementação consumidora.

## Fases

| #   | Fase                                      | Arquivo                                                                                        | Status    |
| --- | ----------------------------------------- | ---------------------------------------------------------------------------------------------- | --------- |
| 01  | Tracer bullet de cadastro e consulta      | [fase-01-tracer-bullet-cadastro-consulta.md](fases/fase-01-tracer-bullet-cadastro-consulta.md) | Concluída |
| 02  | Gestão dos itens pendentes                | [fase-02-gestao-itens-pendentes.md](fases/fase-02-gestao-itens-pendentes.md)                   | Concluída |
| 03  | Ciclo da compra e integração com despesas | [fase-03-ciclo-compra-despesas.md](fases/fase-03-ciclo-compra-despesas.md)                     | Concluída |
| 04  | Compartilhamento, desempenho e robustez   | [fase-04-compartilhamento-robustez.md](fases/fase-04-compartilhamento-robustez.md)             | Concluída |

## Dependências e ordem entre as fases

1. A Fase 01 estabelece o modelo, a persistência e o contrato mínimo usados pelas demais.
2. A Fase 02 depende da identidade e da consulta de itens criadas na Fase 01.
3. A Fase 03 depende das operações de pendentes e do serviço de despesas já existente.
4. A Fase 04 depende de todos os fluxos de escrita e leitura para validar autorização e volume sem lacunas.

Não há ciclos. No plano coordenado, cada fase de front-end depende da fase correspondente deste plano estar implementada e aprovada em `review`.

## Marcos de entrega

- Marco 1 — Fase 01 concluída e aprovada: item válido pode ser criado e consultado com total e ordenação corretos.
- Marco 2 — Fase 02 concluída e aprovada: CRUD dos pendentes funciona com validação e isolamento.
- Marco 3 — Fase 03 concluída e aprovada: compra pode ser concluída, vinculada a despesa, revertida e excluída conforme as regras.
- Marco 4 — Fase 04 concluída e aprovada: permissões, centenas de itens, erros e regressões estão verificados.

## Cobertura de requisitos

| Requisitos                                                     | Tarefas                          |
| -------------------------------------------------------------- | -------------------------------- |
| `LCP-BE-01`, `LCP-BE-04`, `LCP-BE-05`, `LCP-BE-06`             | `T01`–`T05`                      |
| `LCP-BE-02`, `LCP-BE-03`                                       | `T06`–`T09`                      |
| `LCP-BE-07`–`LCP-BE-14`, `LCP-BE-17`                           | `T10`–`T16`                      |
| `LCP-BE-15`, `LCP-BE-16`                                       | `T17`                            |
| `EXPECT-BE-01`, `EXPECT-BE-02`, `EXPECT-BE-04`, `EXPECT-BE-05` | `T01`, `T08`, `T10`, `T12`–`T18` |
| `EXPECT-BE-03`                                                 | `T18`                            |

## Riscos e verificações gerais

- Consistência entre item e despesa envolve mais de um registro — provar por teste que falhas não deixam conclusão, vínculo ou exclusão parcialmente confirmados.
- O contexto compartilhado vem de `IUsuarioLogado` — testar proprietário, convidado com edição, convidado com visualização e tentativa sobre item de outro contexto.
- Totais monetários podem sofrer perda de precisão — testar valores fracionários representativos e igualdade dos agregados.
- Consultas sem limite rígido podem degradar — medir com centenas de itens e confirmar índices e projeções adequados.
- O front-end depende do contrato desta API — revisar os DTOs e o OpenAPI antes de liberar cada fase consumidora.

## Estratégia de reversão

- As alterações de aplicação e endpoints podem ser revertidas por fase, preservando coleções ainda não consumidas.
- Antes de publicação, qualquer mudança destrutiva de formato exige compatibilidade de leitura ou migração separada; nenhuma migração destrutiva está prevista neste plano.
- Se o vínculo com despesa apresentar inconsistência, o fluxo de conclusão com despesa deve permanecer indisponível até correção, sem remover despesas já registradas.

## Perguntas que bloqueiam a implementação

| Pergunta | Por que bloqueia                                                                                                      | Status    |
| -------- | --------------------------------------------------------------------------------------------------------------------- | --------- |
| Nenhuma. | Os comportamentos necessários estão definidos no PRD aprovado; edição direta de comprado permanece fora desta versão. | Resolvida |
