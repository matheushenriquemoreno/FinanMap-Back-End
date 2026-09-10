# Review — Lista de Compras Planejadas (Back-end)

| Status       | Aprovado com ressalvas |
| ------------ | ---------------------- |
| Created      | 2026-09-09             |
| Last Updated | 2026-09-09             |

**Escopo revisado:** implementação completa — Fases 01 a 04, T01 a T18
**Versão da avaliação:** 5
**Snapshot revisado:** `12f83c1` mais a documentação de execução presente no working tree e a validação integrada local de 2026-09-09

## Artefatos analisados

- PRD: `.specs/lista-compras-planejadas/PRODUCT-REQUIREMENTS.md` (Aprovado).
- Plano, fases e estado: `.specs/lista-compras-planejadas/IMPLEMENTATION-PLAN.md`,
  `fases/*.md` e `fases/IMPLEMENTATION-STATE.md`.
- Código de Domain, Application, Infra.data, WebApi e testes da compra planejada.
- Fluxo existente de despesas, incluindo `DespesaService.Adicionar` e exclusão.
- Histórico deste arquivo e as correções registradas nas revisões anteriores.

## Resumo executivo

As quatro fases do back-end foram reavaliadas contra os 17 requisitos e as cinco
expectativas do PRD. O ciclo pendente/comprado, vínculo opcional de despesa,
reversão, preservação na exclusão, autorização contextual e agregados estão
implementados e cobertos por testes; a janela conhecida de leitura do acumulado
após inserção foi eliminada movendo essa leitura para antes do `Add`. O veredito
é **Aprovado com ressalvas**: os gates locais e o smoke integrado autenticado
passam; permanecem como ressalvas a inspeção dedicada do plano de consulta e as
métricas formais de latência/memória em carga.

## Resultado das verificações obrigatórias

| Verificação                         | Resultado                                                            | Evidência                                                                                                                                                                                                                                   |
| ----------------------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Requisitos e critérios de aceitação | Atendida                                                             | Matriz abaixo; endpoints, entidade, serviço, repositório e testes cobrem T01–T18.                                                                                                                                                           |
| Regressão completa                  | Atendida                                                             | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 66 aprovados, 0 falhas.                                                                                                                                         |
| Build                               | Atendida                                                             | `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — 0 avisos e 0 erros.                                                                                                                                          |
| Formato                             | Atendida com ressalva                                                | `dotnet format ... --verify-no-changes --include` limitado aos arquivos da feature passou; o format-check global continua afetado por whitespace preexistente fora da feature.                                                              |
| Complexidade ciclomática            | Atendida com justificativa                                           | Contagem manual das funções novas/alteradas: `Concluir` 20 e `Reverter` 12; ambas estão no limite de 20 e concentram protocolo de validação/compensação coberto pelos testes; auxiliares permanecem abaixo de 10. Nenhuma função excede 20. |
| Complexidade algorítmica            | Atendida                                                             | Consultas usam filtro contextual, índice composto e uma ordenação; agregados são somas lineares e não há I/O dentro de loop por item.                                                                                                       |
| Escopo e arquitetura                | Atendida                                                             | Camadas existentes preservadas; integração usa gateway sobre `IDespesaService`; não foram adicionados limites, migrações ou operações fora do PRD.                                                                                          |
| Autorização e isolamento            | Atendida                                                             | `PodeEditar`, `IdContextoDados` e filtro por proprietário/estado são exercitados na matriz proprietário/editor/visualizador.                                                                                                                |
| Smoke Mongo/HTTP e métricas         | Smoke HTTP/Mongo integrado executado; métricas formais não coletadas | Healthcheck 200, CORS preflight 204, rota protegida 401, autenticação por e-mail/código e persistência/limpeza verificadas contra API local e Mongo em Docker.                                                                              |
| `git diff --check`                  | Atendida                                                             | Sem erros no working tree revisado.                                                                                                                                                                                                         |

## Matriz de rastreabilidade

| Requisito                                        | Código                                                   | Teste/evidência                                                                                            | Status                                                                                      |
| ------------------------------------------------ | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| LCP-BE-01 — cadastrar pendente                   | `CompraPlanejadaService.Adicionar`, DTO e endpoint POST  | `CompraPlanejadaServiceTests`; build da WebApi                                                             | Comprovado                                                                                  |
| LCP-BE-02 — alterar pendente                     | `Atualizar`, `AtualizarSePendente` e PUT contextual      | `CompraPlanejadaServiceTests`, incluindo todos os campos e links                                           | Comprovado                                                                                  |
| LCP-BE-03 — excluir pendente                     | `Excluir` e DELETE contextual                            | testes de exclusão, repetição e total subsequente                                                          | Comprovado                                                                                  |
| LCP-BE-04 — múltiplos links                      | `LinkLojaCompraPlanejada`, `CriarLinks` e mapping        | testes de links válidos, inválidos e substituição                                                          | Comprovado                                                                                  |
| LCP-BE-05 — total de pendentes                   | `ListarPendentes` e `Sum(ValorEstimado)`                 | testes de vazio, decimal e massa                                                                           | Comprovado                                                                                  |
| LCP-BE-06 — ordem dos pendentes                  | `ListarPorEstado` com prioridade/data descendentes       | teste de prioridade e recência; massa de 400 itens                                                         | Comprovado                                                                                  |
| LCP-BE-07 — concluir com valor/data válidos      | `Concluir` e `MarcarComoComprado`                        | conclusão válida, zero, data futura e repetição                                                            | Comprovado                                                                                  |
| LCP-BE-08 — separar pendente/comprado            | estados da entidade e `GetPendentes`/`GetComprados`      | ciclo e consulta exclusiva de comprados                                                                    | Comprovado                                                                                  |
| LCP-BE-09 — criar e vincular despesa             | `CompraPlanejadaDespesaGateway`, DTO e `VincularDespesa` | valor real, mês, ano, categoria e vínculo verificados                                                      | Comprovado                                                                                  |
| LCP-BE-10 — no máximo uma despesa                | estado `DespesaId` e conclusão única                     | repetição e teste de domínio de vínculo único                                                              | Comprovado                                                                                  |
| LCP-BE-11 — dados dos comprados                  | `ListaComprasCompradasResponseDTO` e mapping             | estimativa, real, data e indicação de vínculo                                                              | Comprovado                                                                                  |
| LCP-BE-12 — reverter compra                      | `Reverter`, `ReverterCompra` e CAS contextual            | reversão simples e restauração dos dados                                                                   | Comprovado                                                                                  |
| LCP-BE-13 — excluir despesa na reversão          | `ReverterCompraPlanejadaDTO` e gateway de exclusão       | casos preservar, excluir e falha de exclusão                                                               | Comprovado                                                                                  |
| LCP-BE-14 — excluir comprado preservando despesa | `Excluir` remove só item/vínculo                         | teste confirma ausência de chamada de exclusão da despesa                                                  | Comprovado                                                                                  |
| LCP-BE-15 — escrita autorizada                   | `PodeEditar` e `IUsuarioLogado`                          | proprietário, editor, visualizador e contexto alheio                                                       | Comprovado                                                                                  |
| LCP-BE-16 — leitura compartilhada                | filtros por `IdContextoDados`                            | visualizador lê pendentes/comprados sem acesso lateral                                                     | Comprovado                                                                                  |
| LCP-BE-17 — totais de comprados                  | `TotalEstimado` e `TotalReal`                            | decimais, vazio, conclusão, reversão, exclusão e massa                                                     | Comprovado                                                                                  |
| EXPECT-BE-01 — precisão BRL                      | `decimal` na entidade, DTOs e `Sum`                      | valores fracionários e agregados exatos                                                                    | Comprovado                                                                                  |
| EXPECT-BE-02 — estados consistentes              | transições encapsuladas e `AtualizarSeEstado`            | falha de persistência, reversão e separação de listas                                                      | Comprovado nos cenários testados                                                            |
| EXPECT-BE-03 — centenas de itens                 | índice composto e consultas lineares                     | `CentenasDeItens_MantemSeparacaoEAgregadosExatos` com 400 itens                                            | Comprovado localmente                                                                       |
| EXPECT-BE-04 — erros distinguíveis               | `Validation`, `Forbidden`, `NotFound` e `Exception`      | validação, autorização, inexistência e falhas de gateway                                                   | Comprovado                                                                                  |
| EXPECT-BE-05 — falha sem confirmação falsa       | leitura do acumulado antes do `Add`, CAS e compensação   | `FalhaAoPersistirVinculo_CompensaDespesaEDeixaPendente`, falha de exclusão e regressão de `DespesaService` | Comprovado nos cenários automatizados; falha transacional formal contra Mongo real pendente |

## Achados

| ID   | Severidade         | Achado                                                                                                                                     | Evidência                                                                                                            | Impacto                                                                                                                                            | Recomendação                                                                                                         | Encaminhamento                                             |
| ---- | ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| A-01 | Baixo, operacional | Smoke integrado foi executado com um usuário de teste; não foram coletadas métricas formais de carga nem feita inspeção dedicada do plano. | API local em Docker, Mongo local e navegador autenticado; 66 testes automatizados também passam.                     | Persistência e fluxos funcionais têm evidência integrada, mas volume, latência/memória e comportamento de deployment continuam sem medição formal. | Reexecutar smoke nos três perfis, inspeção de índice e métricas antes da publicação.                                 | Operação/pré-publicação                                    |
| A-02 | Informativo        | A consistência entre documentos usa compensação, não transação distribuída.                                                                | `Concluir` cria a despesa, confirma o item por CAS e compensa falhas; leitura do acumulado ocorre antes da inserção. | Uma indisponibilidade simultânea durante a compensação exige reconciliação operacional; não há evidência de ocorrência nos cenários testados.      | Manter telemetria/reconciliação no desenho de produção; se a garantia atômica virar requisito, abrir design técnico. | Operação ou `create-technical-design` se o requisito mudar |

## Riscos residuais e ressalvas aceitas

- O smoke autenticado e a persistência Mongo foram verificados localmente em
  Docker; plano de consulta e métricas formais ainda são ressalvas operacionais
  e devem ser resolvidos antes da publicação.
- O format-check global inclui whitespace preexistente fora do escopo; o
  conjunto de arquivos da feature passou sem alterações.
- A revisão local aceita a compensação implementada como estratégia desta
  versão; o risco residual de falha simultânea de compensação não é tratado como
  confirmação de sucesso e permanece explicitamente operacional.

## Veredito

**Veredito:** Aprovado com ressalvas.

**Fundamentação:** todos os requisitos e expectativas possuem implementação e
evidência local; os achados anteriores sobre a leitura pós-inserção, o fake que
ignorava o estado esperado e a ausência de cenário positivo de editor foram
encerrados no estado atual. O smoke integrado confirmou healthcheck, CORS,
autorização, login por código, persistência, atualização, conclusão com despesa,
reversão e limpeza no Mongo. Não há achado bloqueador ou alto sem tratamento. As
ressalvas restantes são a inspeção dedicada do plano e as métricas formais.

## Próxima ação

Trabalho concluído no escopo local do plano. Antes de publicar, executar a
inspeção do índice composto e a medição de volume/latência em ambiente integrado.

## Histórico de revisões anteriores

| Versão | Data       | Veredito               | Resumo                                                                                                                                                                            |
| ------ | ---------- | ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1      | 2026-09-09 | Aprovado com ressalvas | Review local da Fase 01, com ressalva operacional do Mongo.                                                                                                                       |
| 2      | 2026-09-09 | Aprovado com ressalvas | Reavaliação da Fase 01 após correções de nulo e cobertura.                                                                                                                        |
| 3      | 2026-09-09 | Aprovado com ressalvas | Review independente da Fase 02; `MatchedCount` contextual confirmado e 50 testes aprovados.                                                                                       |
| 4      | 2026-09-09 | Aprovado com ressalvas | Review final das quatro fases; 66 testes, build e format-check da feature aprovados; smoke/Mongo permanecem operacionais.                                                         |
| 5      | 2026-09-09 | Aprovado com ressalvas | Validação integrada autenticada em API/Mongo Docker e navegador; CRUD, conclusão com despesa, reversão, preservação e limpeza confirmados; métricas formais permanecem pendentes. |
