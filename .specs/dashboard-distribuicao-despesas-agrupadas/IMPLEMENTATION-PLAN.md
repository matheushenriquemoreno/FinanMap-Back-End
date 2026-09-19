# Plano de Implementação — Dashboard: Distribuição de despesas agrupadas

| Status       | Aprovado    |
|--------------|-------------|
| Created      | 2026-09-14  |
| Last Updated | 2026-09-14  |

PRD de referência: `.specs/dashboard-distribuicao-despesas-agrupadas/PRODUCT-REQUIREMENTS.md` (Aprovado)

Design técnico: dispensado. Premissas no lugar:

- O cálculo é feito em memória (C#) a partir das despesas do período, porque `IdDespesaAgrupadora` é persistido como texto simples (não ObjectId) e um `$lookup` no Mongo exigiria conversão (`$toObjectId`). O volume é mensal/poucos meses por usuário.
- A regra de negócio fica numa função pura no domínio (`Domain/Dashboard`), testável sem Mongo; o repositório em `Infra.data` apenas orquestra consulta, resolução de nomes de categoria e montagem do modelo.
- Os nomes das categorias continuam sendo resolvidos na coleção `Categoria`, preservando o comportamento atual em que categorias não encontradas são descartadas.
- Restrição observada: não existe infraestrutura de teste de integração com Mongo no projeto; a cobertura automatizada recai sobre a função pura, e a integração do repositório é verificada manualmente/por inspeção.

## Histórico de atualizações

| Data       | Alteração |
|------------|-----------|
| 2026-09-14 | Versão inicial derivada do PRD aprovado. |
| 2026-09-14 | Plano aprovado (Gate 3). Fase única com T01 (regra pura), T02 (integração no repositório) e T03 (testes). |

## Objetivo geral da implementação

Fazer a distribuição por categoria de despesas do dashboard representar também as despesas que estão dentro das agrupadoras, sem duplicar valores, mantendo a soma das categorias igual ao total de despesas do resumo e preservando o contrato atual da API.

## Estratégia de execução

Trabalho pequeno e coeso, concentrado em uma única capacidade: corrigir o cálculo da distribuição por categoria de Despesa. Por isso há **uma única fase**, que prova o caminho de ponta a ponta (`/api/dashboard/categorias?tipo=Despesa`) e já entrega os testes da regra. A regra é isolada numa função pura (testável sem Mongo) antes de ser plugada no repositório, reduzindo o risco da parte não coberta por teste automatizado.

## Fases

| #  | Fase | Arquivo | Status |
|----|------|---------|--------|
| 01 | Distribuição de despesas agrupadas por categoria | [fase-01-distribuicao-despesas-agrupadas.md](fases/fase-01-distribuicao-despesas-agrupadas.md) | Concluída |

## Dependências e ordem entre as fases

Há apenas uma fase, sem dependência entre fases. Internamente: `T01` (regra pura) precede `T02` (integração no repositório), que precede `T03` (testes da regra e cenários de consistência).

## Marcos de entrega

- Marco 1 — Fase 01 concluída e aprovada em review: `/api/dashboard/categorias?tipo=Despesa` passa a exibir as categorias das despesas filhas e o valor próprio da agrupadora, com a soma das categorias igual ao total de despesas do resumo.

## Riscos e verificações gerais

- Dados legados inconsistentes (agrupadora cujo valor não corresponde a valor próprio + filhas) podem fazer a soma das categorias divergir do resumo — verificado por conferência da soma em cenário de teste e inspeção em dados reais.
- A alteração do repositório depende de acesso a Mongo e não tem teste automatizado no projeto — verificado por revisão de código e, quando possível, validação local contra um período conhecido.
- Valor próprio negativo por inconsistência — verificado por teste de borda; decisão de clampar permanece pergunta em aberto não bloqueante do PRD.
- A ordenação decrescente (`DASH-08`, Desejável) não deve alterar o significado dos dados nem quebrar o front, que não depende de ordem.

## Perguntas que bloqueiam a implementação

| Pergunta | Por que bloqueia | Status |
|----------|------------------|--------|
| Nenhuma. | As perguntas do PRD ficaram registradas como não bloqueantes. | — |
