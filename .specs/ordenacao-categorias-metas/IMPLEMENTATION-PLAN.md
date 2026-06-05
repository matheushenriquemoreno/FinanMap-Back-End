# Implementation Plan — Ordenacao de Categorias e Metas

| Field        | Value                         |
| ------------ | ----------------------------- |
| Tech Lead    | @Usuario                      |
| Team         | Solo developer                |
| Epic/Ticket  | Ordenacao de Categorias e Metas |
| Status       | Draft                         |
| Created      | 2026-06-05                    |
| Last Updated | 2026-06-05                    |

## Overview

Aplicar ordenacoes deterministicas nas listagens existentes sem alterar contratos HTTP ou DTOs:

- categorias recem-criadas devem aparecer primeiro em `GET /api/Categorias/GetUserCategorias`;
- metas nao concluidas devem aparecer antes das concluidas em `GET /api/MetasFinanceiras/`.

A implementacao sera feita em duas fatias verticais pequenas e independentes. A ordenacao de categorias ficara no repositorio MongoDB, usando o `Id` do tipo `ObjectId` em ordem decrescente, pois `Categoria` nao possui `DataCriacao` e o `ObjectId` registra temporalmente a criacao. A ordenacao de metas ficara no servico de aplicacao, pois `Concluida` e uma propriedade calculada a partir das contribuicoes e nao e persistida no MongoDB.

Dentro de cada grupo de metas, sera usado `DataCriacao` decrescente como criterio secundario, mantendo as metas mais recentes primeiro e garantindo uma resposta estavel.

**Technical Design**: Not provided

## Implementation Phases

### Phase 1: Tracer Bullet — Categorias mais recentes primeiro

**Goal**: Ao cadastrar uma categoria e consultar a listagem do mesmo tipo, a nova categoria aparece no topo.

**Vertical slice**: Infra.data (`CategoriaRepository`) -> Application (`CategoriaService`, sem mudanca de contrato) -> WebApi (`GET /api/Categorias/GetUserCategorias`, sem mudanca de endpoint).

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| ORDM-P1-01 | Alterar `CategoriaRepository.GetCategorias(TipoCategoria, string, string)` para aplicar `SortByDescending(x => x.Id)` antes de `ToListAsync()`, preservando os filtros atuais de tipo, usuario e nome. | @Usuario | 20min |
| ORDM-P1-02 | Validar que o serializer de `EntityBase.Id` continua tratando o campo como `ObjectId`, garantindo que a ordenacao decrescente represente a ordem de criacao inclusive para categorias existentes. | @Usuario | 10min |
| ORDM-P1-03 | Executar build da solucao e validar manualmente a listagem de categorias com e sem filtro por nome. | @Usuario | 30min |

**Testing**:

- **Build**: executar `dotnet build FinancasPessoais.sln`.
- **Manual via API**: criar pelo menos tres categorias do mesmo tipo em sequencia e confirmar que o GET retorna a ultima criada primeiro.
- **Regressao manual**: confirmar que os filtros por `tipoCategoria`, `nome` e usuario de contexto continuam funcionando.
- **Compartilhamento**: em modo compartilhado, confirmar que a ordenacao usa apenas as categorias do usuario de contexto.

**Acceptance Criteria**:

- [ ] A categoria criada por ultimo aparece na primeira posicao da listagem do seu tipo.
- [ ] Categorias existentes tambem sao retornadas da mais recente para a mais antiga.
- [ ] O filtro por nome nao altera a regra de ordenacao.
- [ ] Nenhum endpoint, DTO ou formato de resposta e alterado.
- [ ] A solucao compila sem novos erros.

**Dependencies**: MongoDB deve armazenar os IDs de `Categoria` como `ObjectId`, conforme `EntityBaseMapping` existente.

---

### Phase 2: Metas nao concluidas primeiro

**Goal**: Ao listar metas financeiras, todas as metas ainda nao concluidas aparecem antes das metas concluidas.

**Vertical slice**: Infra.data (`MetaFinanceiraRepository`, consulta existente) -> Application (`MetaFinanceiraService.ObterTodas`) -> WebApi (`GET /api/MetasFinanceiras/`, sem mudanca de contrato).

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| ORDM-P2-01 | Alterar `MetaFinanceiraService.ObterTodas()` para ordenar a lista carregada por `Concluida` ascendente (`false` antes de `true`) e depois por `DataCriacao` decrescente, antes de mapear para `ResultMetaFinanceiraDTO`. | @Usuario | 20min |
| ORDM-P2-02 | Confirmar que adicionar, editar ou remover uma contribuicao muda automaticamente a posicao da meta na proxima listagem quando o valor atual cruza o valor alvo. | @Usuario | 20min |
| ORDM-P2-03 | Executar build e validar manualmente os cenarios de meta ativa, meta concluida e meta que deixa de estar concluida apos remocao/edicao de contribuicao. | @Usuario | 40min |

**Testing**:

- **Build**: executar `dotnet build FinancasPessoais.sln`.
- **Manual via API**: criar metas nao concluidas e concluidas e confirmar que todas as nao concluidas aparecem primeiro.
- **Ordenacao secundaria**: dentro dos grupos de metas nao concluidas e concluidas, confirmar `DataCriacao` decrescente.
- **Mudanca de estado**: concluir uma meta por contribuicao e confirmar que ela passa para o grupo final; remover ou reduzir a contribuicao e confirmar que ela retorna ao grupo inicial.
- **Regressao manual**: confirmar que `GET /api/MetasFinanceiras/resumo` mantem os mesmos totais, pois a ordenacao nao deve alterar calculos.

**Acceptance Criteria**:

- [ ] Nenhuma meta concluida aparece antes de uma meta nao concluida.
- [ ] Metas do mesmo estado aparecem da mais recente para a mais antiga.
- [ ] A posicao da meta reflete o estado calculado atual das contribuicoes na proxima consulta.
- [ ] O resumo de metas e os demais endpoints permanecem inalterados.
- [ ] A solucao compila sem novos erros.

**Dependencies**: Phase 1 concluida apenas para facilitar validacao e entrega incremental; tecnicamente, as alteracoes sao independentes.

## Milestones

| Milestone | Target Date | Description |
|-----------|-------------|-------------|
| Categorias ordenadas | Sem deadline | Categorias mais recentes retornadas primeiro, com filtros preservados |
| Listagens ordenadas | Sem deadline | Categorias e metas retornadas conforme as novas regras |

## Dependencies

| Dependency | Type | Owner | Status | Risk if Delayed |
|------------|------|-------|--------|-----------------|
| MongoDB e `EntityBaseMapping` | Technical | @Usuario | Disponivel | A ordenacao temporal de categorias depende de IDs persistidos como `ObjectId` |
| Calculo de `MetaFinanceira.Concluida` | Internal | @Usuario | Disponivel | A ordenacao de metas depende do valor atual calculado pelas contribuicoes |
| Ambiente autenticado para validacao manual | Technical | @Usuario | A confirmar | Sem usuario e dados de teste, a verificacao ponta a ponta fica limitada ao build |

## Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Alguma categoria legada possuir ID que nao seja `ObjectId` | Medio | Baixa | Inspecionar amostra da collection antes do deploy; se houver IDs fora do padrao, adicionar `DataCriacao` e realizar backfill em plano separado |
| Ordenar metas em memoria aumentar custo para usuarios com volume muito alto | Baixo | Baixa | Manter a mudanca simples agora; monitorar volume e considerar persistir estado/indexar apenas se houver necessidade comprovada |
| Ausencia de testes automatizados permitir regressao futura | Medio | Media | Executar cenarios manuais documentados e considerar criar projeto de testes de integracao em iniciativa separada |
| Criterio secundario nao corresponder a expectativa futura do produto | Baixo | Media | Documentar `DataCriacao` decrescente como desempate e ajustar depois sem alterar o criterio principal |

## Testing Strategy

O repositorio nao possui projeto de testes automatizados na solucao atual. Para esta melhoria de baixo impacto, a validacao sera composta por build e testes manuais dos endpoints existentes.

- **Compilacao**: executar `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`.
- **API**: validar as respostas com usuario autenticado e dados criados em sequencia conhecida.
- **Persistencia**: confirmar no MongoDB que IDs de categorias sao `ObjectId` e que `DataCriacao` das metas esta preenchida.
- **Regressao**: validar filtros de categorias, contexto compartilhado, resumo de metas e mudancas de conclusao causadas por contribuicoes.
- **Dados de teste**: usar ao menos tres categorias do mesmo tipo e quatro metas, sendo duas concluidas e duas nao concluidas, criadas em horarios distintos.

## Rollback Plan

As alteracoes nao exigem migracao de banco, novos campos, novos indices ou mudancas de contrato.

- **Gatilho de rollback**: listagens retornando dados incorretos, filtros deixando de funcionar ou degradacao perceptivel na listagem de metas.
- **Rollback imediato**: reverter somente as chamadas de ordenacao adicionadas em `CategoriaRepository.GetCategorias` e `MetaFinanceiraService.ObterTodas`.
- **Banco de dados**: nenhuma acao necessaria, pois os documentos nao serao modificados.
- **Pos-rollback**: reproduzir o problema com os dados afetados, ajustar o criterio de ordenacao, executar novamente o build e os cenarios manuais antes do redeploy.

## Validation Checklist

- [x] Technical design referenced if available — nao existe para esta melhoria
- [x] Phase 1 e um tracer bullet com valor ponta a ponta
- [x] Todas as fases possuem goal, tasks, testing, acceptance criteria e dependencies
- [x] IDs seguem o formato `ORDM-PN-NN` e sao unicos
- [x] Testing esta embutido em cada fase
- [x] Nenhuma fase e apenas setup ou infraestrutura
- [x] Milestones definidos
- [x] Dependencies listadas com responsaveis e riscos
- [x] Risks possui pelo menos tres entradas
- [x] Testing Strategy presente
- [x] Rollback Plan presente
