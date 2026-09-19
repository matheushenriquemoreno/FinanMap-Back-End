# Dashboard — Distribuição de despesas agrupadas — Back-end

| Status       | Aprovado     |
|--------------|--------------|
| Created      | 2026-09-14   |
| Last Updated | 2026-09-14   |

## Histórico de atualizações

| Data       | Alteração |
|------------|-----------|
| 2026-09-14 | Versão inicial. Decisão confirmada: a agrupadora entra na distribuição pelo seu valor próprio (Base) e cada despesa filha entra pela sua própria categoria, sem duplicar valores. |
| 2026-09-14 | PRD aprovado (Gate 1). Perguntas em aberto registradas como não bloqueantes. |

## Visão geral

A distribuição por categoria do dashboard (`/api/dashboard/categorias` para o tipo Despesa) hoje atribui todo o valor de uma despesa agrupada à categoria da agrupadora e não representa as categorias reais das despesas filhas. Isso produz um retrato incorreto de "onde o dinheiro foi gasto", mesmo quando o total de despesas do período está correto.

Este documento define o comportamento esperado para que a distribuição por categoria reflita também as despesas que estão dentro das agrupadoras, preservando a regra do domínio em que o valor da agrupadora é a soma do seu valor próprio com o das filhas. O escopo é o back-end do dashboard; o contrato de resposta e o front-end não mudam.

## Problema e impacto

**Problema:** ao consultar a distribuição por categoria de despesas, o usuário vê o valor da agrupadora concentrado na categoria dela, enquanto as categorias das despesas filhas ficam ausentes do gráfico e do ranking. A leitura por categoria fica enganosa.

**Quem é afetado:** proprietários de dados financeiros e convidados autorizados que consultam o dashboard, sempre que o período inclui despesas agrupadas.

**Impacto se não for resolvido:** o usuário toma decisões com uma distribuição de gastos incorreta — categorias superestimadas (a da agrupadora) e categorias reais subestimadas ou invisíveis —, ainda que o total de despesas do resumo esteja certo. A perda de confiança na informação do dashboard compromete o próprio propósito do recurso.

## Usuários e perfis afetados

| Perfil | Contexto de uso | Necessidade principal |
|--------|-----------------|----------------------|
| Proprietário da conta | Consulta o dashboard do próprio período com despesas agrupadas | Enxergar a distribuição de gastos por categoria incluindo as despesas dentro das agrupadoras |
| Convidado autorizado (visualização ou edição) | Consulta o dashboard no contexto compartilhado do proprietário | Obter a mesma distribuição por categoria do contexto autorizado |

## Objetivos e critérios de sucesso

| Objetivo | Critério de sucesso | Forma de verificação |
|----------|--------------------|----------------------|
| Representar as categorias reais das despesas agrupadas | Cada despesa filha contribui para a sua própria categoria na distribuição | Consulta com agrupadora e filhas de categorias distintas retorna ambas as categorias |
| Não duplicar valores entre agrupadora e filhas | A soma dos valores das categorias é igual ao total de despesas do período do resumo | Comparar a soma da distribuição com o total de `/api/dashboard/resumo` no mesmo período |
| Preservar o valor próprio da agrupadora | A agrupadora contribui na sua categoria pelo valor próprio (valor total menos soma das filhas) | Consulta com agrupadora de valor próprio maior que zero retorna a categoria dela com o valor próprio |
| Manter o contrato existente | A resposta permanece com categoria, valor, tipo e percentual, sem mudança de forma | Chamada a `/api/dashboard/categorias?tipo=Despesa` mantém o mesmo formato |

## Escopo e não objetivos

### Dentro do escopo

- Ajustar o cálculo da distribuição por categoria para o tipo Despesa em `/api/dashboard/categorias`.
- Incluir as despesas filhas na categoria de cada uma.
- Contribuir com a agrupadora pelo valor próprio (valor total menos a soma das filhas) na categoria dela.
- Recalcular o percentual de cada categoria sobre o total de despesas do período resultante.
- Manter a forma atual da resposta (categoria, valor, tipo, percentual).

### Fora do escopo

- Totais de `/api/dashboard/resumo` e `/api/dashboard/evolucao` — já somam as agrupadoras com o valor das filhas e não apresentam informação incorreta.
- Relatório de Acumulado Mensal (`AcumuladoMensalRepository`) — usa filtro semelhante, mas não foi solicitado nesta iniciativa.
- Distribuições de Rendimento e Investimento — não possuem conceito de agrupamento.
- Mudanças de contrato, DTO ou front-end — a correção preserva o formato atual e o front não precisa ser alterado.
- Alterar o modelo de dados, a regra de agrupamento ou o valor persistido da agrupadora.
- Listagem de despesas do mês (`GetPeloMes`) — a ocultação das filhas é intencional e a expansão é feita pelo front.

### Adiado

- Revisar o Acumulado Mensal sob a mesma ótica — postergado por decisão de escopo; pode gerar nova iniciativa se surgir a necessidade.

## Requisitos funcionais

Prioridades: **Essencial** (bloqueia a entrega), **Importante** (deve entrar), **Desejável** (entra se couber, sem comprometer os demais).

- **Essencial** `DASH-01` A distribuição por categoria de Despesa deve atribuir o valor de cada despesa filha à categoria da própria filha.
- **Essencial** `DASH-02` A distribuição por categoria de Despesa deve atribuir à categoria da agrupadora o seu valor próprio, calculado como o valor total da agrupadora menos a soma dos valores das suas filhas.
- **Essencial** `DASH-03` A distribuição por categoria de Despesa deve atribuir o valor integral às despesas que não são filha nem agrupadora com filhas.
- **Essencial** `DASH-04` A soma dos valores das categorias deve ser igual ao total de despesas do período retornado pelo resumo.
- **Essencial** `DASH-05` O percentual de cada categoria deve ser calculado sobre o total de despesas do período resultante da distribuição.
- **Importante** `DASH-06` Uma despesa filha cuja agrupadora não esteja presente no período consultado deve ser atribuída integralmente à categoria da própria filha.
- **Importante** `DASH-07` A resposta deve manter os campos categoria, valor, tipo e percentual, sem alteração de forma em relação ao contrato atual.
- **Desejável** `DASH-08` A distribuição deve ser retornada em ordem decrescente de valor.

## Expectativas não funcionais

- **EXPECT-01** Valores monetários devem conservar precisão compatível com a exibição em Real (BRL).
- **EXPECT-02** A distribuição deve ser determinística para o mesmo conjunto de dados e período.
- **EXPECT-03** A consulta deve permanecer utilizável com o volume de despesas de um período mensal ou de poucos meses por usuário.
- **EXPECT-04** A correção não deve alterar o resultado das distribuições de Rendimento e Investimento.

## Regras de negócio e restrições

### Regras de negócio

- O valor de uma agrupadora é a soma do seu valor próprio com o valor das suas despesas filhas (BR-011 do domínio).
- Uma despesa filha pertence a uma agrupadora e compartilha o mês/ano dela.
- Uma agrupadora pode ter valor próprio maior que zero, que é uma despesa legítima e deve continuar representada.
- As despesas filhas e as despesas sem agrupamento são despesas legítimas e devem ser contabilizadas uma única vez.

### Restrições

- O escopo é o back-end do dashboard; não há mudança de contrato nem de front-end.
- O identificador da agrupadora nas filhas é armazenado como texto simples, distinto do identificador Mongo das transações — a implementação deve lidar com essa diferença ao correlacionar filhas e agrupadoras.
- A correção deve preservar o comportamento de isolamento por usuário/contexto compartilhado já aplicado às consultas do dashboard.

## Premissas

- Os dados de agrupamento são consistentes, isto é, o valor da agrupadora corresponde ao valor próprio mais a soma das filhas — risco: dados legados inconsistentes podem fazer a soma das categorias divergir do total do resumo.
- O valor próprio da agrupadora (valor total menos a soma das filhas) é maior ou igual a zero — risco: um valor próprio negativo seria subtraído da categoria da agrupadora.
- Todas as filhas de uma agrupadora compartilham o mês/ano da agrupadora — risco: uma filha em mês distinto não entraria no cálculo do valor próprio do período.
- O front-end apenas consome categoria e valor, sem depender de ordenação específica — risco: baixo; a ordenação é apenas uma melhoria de exibição.

## Fluxos e casos de borda

### Fluxos principais

- **Consultar distribuição com agrupadora:** o período contém uma agrupadora com valor próprio e filhas; a agrupadora aparece pela sua categoria com o valor próprio e cada filha aparece pela sua categoria com o próprio valor.
- **Consultar distribuição sem agrupamento:** despesas comuns aparecem integralmente em suas categorias, sem alteração em relação ao comportamento atual.
- **Consultar distribuição com múltiplas agrupadoras:** cada agrupadora e suas respectivas filhas são tratadas de forma independente.

### Estados vazios

- Um período sem despesas retorna coleção vazia, sem erro.

### Erros e falhas

- Dados inconsistentes de agrupamento não devem interromper a consulta; a distribuição é calculada com o que existe.
- Uma filha sem agrupadora correspondente no período é tratada como despesa comum (DASH-06).

### Limites

- Períodos de um único mês ou de múltiplos meses devem produzir distribuições consistentes com os respectivos totais do resumo.
- Não há limite rígido de agrupadoras, filhas ou categorias por período nesta versão.

## Critérios de aceitação

1. Uma agrupadora com valor próprio maior que zero e uma filha em categoria diferente resulta em duas categorias: a da agrupadora com o valor próprio e a da filha com o valor dela.
2. A soma dos valores das categorias é igual ao total de despesas do resumo para o mesmo período e usuário.
3. Despesas sem agrupamento continuam aparecendo integralmente em suas categorias.
4. O percentual de cada categoria corresponde à sua participação no total de despesas do período.
5. A resposta de `/api/dashboard/categorias?tipo=Despesa` mantém o formato atual (categoria, valor, tipo, percentual).
6. Uma filha cuja agrupadora não está presente no período é contabilizada integralmente na categoria dela.
7. As distribuições de Rendimento e Investimento permanecem inalteradas.

## Perguntas em aberto

| Pergunta | Por que importa | Status |
|----------|-----------------|--------|
| Quando o valor próprio calculado for negativo por dados inconsistentes, o valor deve ser limitado a zero? | Define se a distribuição pode exibir valor negativo na categoria da agrupadora. | Aberta, não bloqueante |
| Categorias que não existem mais na coleção de Categoria devem ser mantidas como "Sem Categoria" ou descartadas? | Hoje as categorias não encontradas são descartadas; mudar isso afeta o que o usuário vê. | Aberta, não bloqueante |
