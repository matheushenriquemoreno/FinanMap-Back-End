# FinanMap

FinanMap e um contexto de gerenciamento de financas pessoais voltado ao controle mensal de rendimentos, despesas, investimentos e compromissos financeiros planejados.

## Linguagem

**Despesa**:
Uma saida financeira mensal ja representada em um mes e ano especificos. Uma **Despesa** pode ser criada manualmente ou gerada a partir de um **Custo Fixo**.
_Evitar_: Custo fixo, conta fixa, compromisso fixo

**Custo Fixo**:
Um compromisso financeiro recorrente usado para lembrar o usuario de um vencimento mensal, incluindo nome, dia de vencimento, status e categoria opcional. Um **Custo Fixo** nao possui valor e nao e, por si so, uma **Despesa** mensal.
_Evitar_: Despesa recorrente, despesa fixa, parcelamento

**Dia de Vencimento**:
O dia do calendario em que um **Custo Fixo** normalmente vence em cada mes.
_Evitar_: Data de pagamento, data de criacao

**Despesa Recorrente**:
Uma sequencia de **Despesas** mensais geradas em lote a partir de um fluxo de despesa existente. Ela representa lancamentos mensais, nao o modelo permanente de um compromisso fixo.
_Evitar_: Custo fixo

**Despesa Parcelada**:
Uma sequencia de **Despesas** mensais que divide o valor total de uma compra em uma quantidade definida de parcelas.
_Evitar_: Custo fixo, assinatura

## Ambiguidades Mapeadas

**Custo Fixo vs Despesa Recorrente**:
O projeto mantem esses termos separados. Um **Custo Fixo** e o compromisso permanente, como agua vencendo todo dia 5; uma **Despesa Recorrente** e um conjunto de registros mensais de despesa.

## Exemplo de Dialogo

Dev: "Energia vence todo dia 30. Isso entra como despesa recorrente?"

Especialista do dominio: "Nao. Energia e um Custo Fixo. A despesa de maio pode ser gerada a partir dele, mas o cadastro de Energia continua existindo como a regra."

Dev: "Entao uma despesa mensal conhece o custo fixo que a originou?"

Especialista do dominio: "Sim, quando ela tiver sido gerada por um custo fixo. Mas uma despesa manual continua sendo apenas uma despesa."
