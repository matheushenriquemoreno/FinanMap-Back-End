# Implementation State: ordenacao-categorias-metas

## Phase 1 -- Tracer Bullet — Categorias mais recentes primeiro
**Status: completed**
- [x] ORDM-P1-01: Alterar `CategoriaRepository.GetCategorias(TipoCategoria, string, string)` para aplicar `SortByDescending(x => x.Id)` antes de `ToListAsync()`, preservando os filtros atuais de tipo, usuario e nome.
- [x] ORDM-P1-02: Validar que o serializer de `EntityBase.Id` continua tratando o campo como `ObjectId`, garantindo que a ordenacao decrescente represente a ordem de criacao inclusive para categorias existentes.
- [x] ORDM-P1-03: Executar build da solucao e validar manualmente a listagem de categorias com e sem filtro por nome.

## Phase 2 -- Metas nao concluidas primeiro
- [ ] ORDM-P2-01: Alterar `MetaFinanceiraService.ObterTodas()` para ordenar a lista carregada por `Concluida` ascendente (`false` antes de `true`) e depois por `DataCriacao` decrescente, antes de mapear para `ResultMetaFinanceiraDTO`.
- [ ] ORDM-P2-02: Confirmar que adicionar, editar ou remover uma contribuicao muda automaticamente a posicao da meta na proxima listagem quando o valor atual cruza o valor alvo.
- [ ] ORDM-P2-03: Executar build e validar manualmente os cenarios de meta ativa, meta concluida e meta que deixa de estar concluida apos remocao/edicao de contribuicao.
