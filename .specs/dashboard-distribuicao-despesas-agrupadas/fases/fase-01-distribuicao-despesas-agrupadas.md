# Fase 01 — Distribuição de despesas agrupadas por categoria

| Status       | Concluída  |
|--------------|------------|
| Created      | 2026-09-14 |
| Last Updated | 2026-09-14 |

**Objetivo e resultado esperado:** a distribuição por categoria de despesas do dashboard passa a incluir as despesas que estão dentro das agrupadoras, atribuindo cada filha à sua própria categoria e a agrupadora ao seu valor próprio, sem duplicar valores.
**Capacidade ou fluxo coberto:** consulta `GET /api/dashboard/categorias?tipo=Despesa` com dados que contenham agrupadoras, filhas e despesas comuns.
**Requisitos relacionados:** `DASH-01`, `DASH-02`, `DASH-03`, `DASH-04`, `DASH-05`, `DASH-06`, `DASH-07`, `DASH-08`.
**Dependências externas:** nenhuma.

## Tarefa T01 — Criar a regra pura de distribuição de despesas por categoria

Criar uma função pura que receba as despesas de um período e devolva o valor a atribuir a cada categoria, aplicando a regra base + filhas. A função calcula, primeiro, a soma das filhas por agrupadora (agrupando por `IdDespesaAgrupadora` as despesas com `EstaAgrupada()` verdadeiro). Em seguida, para cada despesa: se for filha, contribui com o próprio `Valor` na sua `CategoriaId`; se não for filha, contribui com o valor próprio, calculado como `Valor − somaDasFilhasDaAgrupadora` (zero quando não há filhas, o que preserva as despesas comuns integralmente). O resultado é um dicionário `CategoriaId → valor`, ignorando chaves de categoria vazias.

- **Requisitos relacionados:** `DASH-01`, `DASH-02`, `DASH-03`, `DASH-06`
- **Referência ao design:** premissa do plano (regra em função pura no domínio, testável sem Mongo)
- **Dependências:** nenhuma
- **Parte do sistema afetada:** novo arquivo `Modulos/GerenciamentoMensal/Domain/Dashboard/DistribuicaoDespesaCategorias.cs`; entidade `Domain.Entity.Despesa`
- **Testes e verificações:** coberto por `T03` — agrupadora com valor próprio e filha em categoria distinta; despesa comum; filha sem agrupadora no conjunto
- **Critérios de conclusão:** existe uma função estática pública que recebe `IEnumerable<Despesa>` e retorna as contribuições por `CategoriaId`; para uma agrupadora com valor próprio `B` e filhas em categorias distintas, o resultado contém `B` na categoria da agrupadora e o valor de cada filha na sua categoria; despesas comuns aparecem com o valor integral
- **Riscos ou premissas:** assume dados consistentes (valor da agrupadora = valor próprio + soma das filhas); valor próprio negativo é somado como está

## Tarefa T02 — Aplicar a regra na distribuição por categoria do dashboard

Substituir o cálculo do método `ObterDistribuicaoPorCollectionDespesa` em `DashboardRepository` para usar a regra de `T01`. A consulta do período (`CriarFiltroPeriodo<Despesa>`) passa a carregar todas as despesas do usuário no período, sem o filtro `IdDespesaAgrupadora == null`. As contribuições por `CategoriaId` são obtidas da função pura; os nomes das categorias são resolvidos na coleção `Categoria`, mantendo o comportamento atual de descartar categorias não encontradas. O total do período é a soma das contribuições; o percentual de cada categoria é `valor / total * 100` com duas casas; o resultado é ordenado por valor decrescente. O método retorna `CategoriaDashboardModel(categoria, valor, "Despesa", percentual)`, mantendo o formato atual.

- **Requisitos relacionados:** `DASH-04`, `DASH-05`, `DASH-07`, `DASH-08`
- **Referência ao design:** premissa do plano (cálculo em memória por causa do armazenamento textual de `IdDespesaAgrupadora`)
- **Dependências:** `T01`
- **Parte do sistema afetada:** `Modulos/GerenciamentoMensal/Infra.data/Mongo/Repositorys/DashboardRepository.cs` (método `ObterDistribuicaoPorCollectionDespesa`); inalterados: `ObterTotalPeriodoDespesa`, `ObterTotalPeriodoComDiasDespesa`, `ObterDistribuicaoPorCollection<T>` de Rendimento/Investimento
- **Testes e verificações:** coberto por `T03` (regra) e verificação da fase (soma das categorias = total de despesas do resumo; formato de resposta preservado)
- **Critérios de conclusão:** `/api/dashboard/categorias?tipo=Despesa` deixa de filtrar filhas; a agrupadora aparece pelo valor próprio; as filhas aparecem pelas suas categorias; a soma dos valores das categorias é igual ao total de despesas de `/api/dashboard/resumo` no mesmo período; a resposta mantém os campos categoria, valor, tipo e percentual; Rendimento e Investimento permanecem inalterados
- **Riscos ou premissas:** dados legados inconsistentes podem divergir do resumo; a parte de consulta/join não tem teste automatizado no projeto, exigindo inspeção e, quando possível, validação local

## Tarefa T03 — Testes automatizados da distribuição de despesas agrupadas

Criar testes xUnit para a regra de distribuição, cobrindo a agrupadora com valor próprio, as filhas em categorias distintas, a despesa comum e a filha sem agrupadora no conjunto, além da consistência da soma. Os testes constroem `Despesa` com categorias distintas, definem `Id`/`CategoriaId` e as relações de agrupamento, e verificam o dicionário retornado.

- **Requisitos relacionados:** `DASH-01`, `DASH-02`, `DASH-03`, `DASH-04`, `DASH-06`
- **Referência ao design:** premissa do plano (cobertura automatizada sobre a função pura)
- **Dependências:** `T01`, `T02`
- **Parte do sistema afetada:** novo arquivo `Modulos/GerenciamentoMensal/Tests/DashboardDistribuicaoDespesaTests.cs`; projeto `Tests` (xUnit)
- **Testes e verificações:** cenários (1) agrupadora com valor próprio em uma categoria e filha em outra → cada categoria recebe o valor correto; (2) despesa comum → valor integral; (3) filha cuja agrupadora não está no conjunto → valor integral; (4) soma das contribuições igual à soma de (agrupadoras cheias + despesas comuns)
- **Critérios de conclusão:** os testes passam junto com os testes existentes; cada cenário acima tem asserção objetiva própria
- **Riscos ou premissas:** sem infraestrutura de Mongo no projeto, o teste cobre a regra pura e não a consulta do repositório

## Orientações de implementação

- A função pura recebe `Despesa` já carregada; não deve depender de `Categoria` navegável, apenas de `Id`, `CategoriaId`, `Valor` e `IdDespesaAgrupadora`.
- Para identificar filhas, usar `EstaAgrupada()` (`IdDespesaAgrupadora` não vazio). Para identificar agrupadora com filhas, usar a presença do `Id` no mapa de somas das filhas — não depender de `EhAgrupadora()`, pois o flag pode não estar consistente.
- Ignorar no resultado as chaves de `CategoriaId` vazias; a resolução de nome na coleção `Categoria` segue o padrão `BsonDocument` já usado no repositório.
- Manter `ObterTotalPeriodoDespesa` e `ObterTotalPeriodoComDiasDespesa` como estão (totais já corretos).
- Ordenar o resultado por valor decrescente antes de retornar (`DASH-08`).

## Testes e verificações da fase

```powershell
dotnet build  Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore
dotnet test   Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore
dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes --no-restore
git diff --check
```

- Conferência funcional (quando houver ambiente): para um período com agrupadora e filhas, verificar que a soma dos valores de `/api/dashboard/categorias?tipo=Despesa` é igual a `despesa.total` de `/api/dashboard/resumo`, e que a categoria da filha aparece na distribuição.

## Critérios de aceitação da fase

1. Uma agrupadora com valor próprio e uma filha em categoria diferente resultam em duas categorias: a da agrupadora com o valor próprio e a da filha com o valor dela.
2. A soma dos valores das categorias é igual ao total de despesas do resumo para o mesmo período e usuário.
3. Despesas comuns continuam aparecendo integralmente em suas categorias.
4. O percentual de cada categoria corresponde à sua participação no total de despesas do período.
5. A resposta de `/api/dashboard/categorias?tipo=Despesa` mantém o formato atual (categoria, valor, tipo, percentual).
6. Uma filha cuja agrupadora não está presente no período é contabilizada integralmente na categoria dela.
7. As distribuições de Rendimento e Investimento permanecem inalteradas.
8. `dotnet build`, `dotnet test` e `dotnet format --verify-no-changes` passam.

## Riscos, premissas e dependências externas da fase

- Dados inconsistentes de agrupamento podem divergir do resumo — se ocorrer em dados reais, registrar como questão de dados, não de regra.
- Sem teste de integração com Mongo no projeto, a consulta/join do repositório depende de revisão e validação local — se a validação local não for possível, registrar a limitação na conclusão da fase.
- Valor próprio negativo é pergunta em aberto não bloqueante do PRD — implementar somando como está e registrar; se a decisão mudar, ajustar a função e o teste correspondente.

## Registro de execução

| Tarefa | Status | Evidência |
|--------|--------|-----------|
| T01 | Concluída | `Modulos/GerenciamentoMensal/Domain/Dashboard/DistribuicaoDespesaCategorias.cs` (função estática `Calcular(IEnumerable<Despesa>)`); build sem erros. |
| T02 | Concluída | `ObterDistribuicaoPorCollectionDespesa` reescrito em `Modulos/GerenciamentoMensal/Infra.data/Mongo/Repositorys/DashboardRepository.cs` (remove filtro `IdDespesaAgrupadora == null`, usa a regra pura, resolve nomes via `BsonDocument`, ordena decrescente); inspeção de código. |
| T03 | Concluída | `Modulos/GerenciamentoMensal/Tests/DashboardDistribuicaoDespesaTests.cs` com 6 testes; `dotnet test` = 74 aprovados, 0 falhas. |

Verificação da fase:

- `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` → 0 erros.
- `dotnet test Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` → 74/74 aprovados.
- `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes --no-restore` → sem alterações (exit 0).
- `git diff --check` → limpo.

Desvios registrados:

- Não há teste de integração com Mongo no projeto; a consulta/join do repositório (`T02`) foi verificada por inspeção de código. Validação local contra dados reais não foi executada nesta sessão (limitação registrada).
- O `dotnet format --verify-no-changes` do solution falhava por formatação pré-existente em `CompraPlanejadaService.cs`, `CustoFixoLembreteService.cs` e `CachedUsuarioRepositoryTests.cs` (arquivos fora do escopo funcional). Com autorização explícita do usuário, a formatação desses arquivos foi corrigida (mudanças apenas de espaço em branco) para satisfazer o critério de aceitação 8.
