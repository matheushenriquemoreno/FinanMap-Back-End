# Custos Fixos

## Overview

O Custos Fixos é uma funcionalidade do FinanMap que permite ao usuário cadastrar compromissos financeiros recorrentes — como água, energia e internet — associados a um dia de vencimento mensal. O sistema envia lembretes automáticos por e-mail 3 dias antes e no dia do vencimento, consolidando todos os custos fixos aplicáveis em um único e-mail por data de lembrete. A feature resolve o problema de o usuário depender de controles externos ou memória para acompanhar vencimentos fixos.

## Problem Statement

**Problem:** O FinanMap controla rendimentos, despesas e investimentos mensais, mas não possui um lugar próprio para acompanhar compromissos fixos por dia de vencimento. O usuário depende de memória, planilhas, cadernos ou calendários externos para lembrar vencimentos mensais.

**Affected users:** O usuário proprietário dos dados financeiros, que precisa acompanhar compromissos recorrentes como contas de serviços e assinaturas. O problema ocorre mensalmente, a cada ciclo de vencimentos.

**Impact:** Sem esta funcionalidade, o usuário continua exposto ao risco de esquecer vencimentos, especialmente quando o valor ainda não foi lançado como despesa mensal. Isso pode resultar em multas, juros e perda de controle financeiro.

## Target Users

| User | Context | Primary Need |
|------|---------|--------------|
| Usuário proprietário | Acessa o FinanMap para gerenciar finanças pessoais; possui e-mail vinculado à conta | Cadastrar compromissos fixos e receber lembretes automáticos próximos ao vencimento |

## Goals & Success Criteria

| Goal | Success Criterion | How to Measure |
|------|-------------------|----------------|
| Entrega confiável de lembretes | ≥ 95% dos lembretes programados são enviados com sucesso | Razão entre registros de envio com sucesso e total de lembretes programados no log interno de envio |
| Idempotência do fluxo de envio | Zero e-mails duplicados para o mesmo usuário, data de referência e tipo de lembrete | Consulta ao log interno buscando duplicatas por chave de idempotência (usuário + data de referência + tipo de lembrete) |

## Scope

### In Scope

- Cadastro de custo fixo com nome, dia de vencimento (1–31) e categoria opcional.
- Ativação e desativação individual de custos fixos.
- Validação de unicidade: impedir custo fixo ativo com mesmo nome e dia de vencimento para o mesmo usuário.
- Tratamento de dia inexistente no mês: considerar o último dia do mês.
- Envio de lembrete por e-mail 3 dias antes do vencimento.
- Envio de lembrete por e-mail no dia do vencimento.
- Consolidação de todos os custos fixos aplicáveis em um único e-mail por usuário e por data de lembrete.
- Assunto do e-mail variável conforme o tipo de lembrete (antecedência vs. dia do vencimento).
- Corpo do e-mail com formato padrão: nome do custo fixo e quantos dias faltam, sem valores financeiros.
- Idempotência de envio por usuário, data de referência do vencimento e tipo de lembrete.
- Registro interno de envio bem-sucedido; falhas permitem nova tentativa.
- Reexecuções pulam silenciosamente lembretes já enviados com sucesso.
- Opt-out global: o usuário pode desativar todos os lembretes por e-mail de uma vez.
- Estado vazio na tela de custos fixos: mensagem orientando o cadastro quando não há nenhum custo fixo.
- Quando todos os custos fixos estiverem desativados, nenhum e-mail é enviado (silenciosamente).

### Non-Goals

- **Transformação automática de custo fixo em despesa mensal** — o custo fixo é voltado a lembrete, não a previsão financeira; a conversão é uma melhoria futura.
- **Notificação por push (mobile) ou SMS** — o canal desta versão é exclusivamente e-mail.
- **Relatórios ou histórico visível ao usuário dos lembretes enviados** — o histórico de envio é controle interno, sem exposição em tela.
- **Custos fixos com valor monetário** — custo fixo não possui valor; não se destina a previsão financeira.
- **Compartilhamento de custos fixos com usuários convidados** — usuários convidados de conta compartilhada não recebem e-mail de custo fixo.
- **Configuração pelo usuário dos dias de antecedência do lembrete** — a regra é fixa em 3 dias antes e no dia do vencimento.

## Requirements

**Priority labels:**
- **P0** — launch blocker; must ship
- **P1** — high value; should ship
- **P2** — nice to have; revisit later

### Cadastro e Gestão de Custos Fixos

- **P0** `CFIX-01` O sistema deve permitir ao usuário cadastrar um custo fixo com nome e dia de vencimento (1–31).
- **P0** `CFIX-02` O sistema deve permitir ao usuário associar uma categoria de despesa ao custo fixo de forma opcional.
- **P0** `CFIX-03` O sistema deve rejeitar o cadastro de custo fixo quando já existir um custo fixo ativo com o mesmo nome e dia de vencimento para o mesmo usuário.
- **P0** `CFIX-04` O sistema deve permitir ao usuário ativar ou desativar um custo fixo individualmente.
- **P0** `CFIX-05` O sistema deve listar os custos fixos do usuário, indicando o status de cada um (ativo ou inativo).
- **P1** `CFIX-06` O sistema deve permitir ao usuário editar o nome, dia de vencimento e categoria de um custo fixo existente.
- **P0** `CFIX-07` O sistema deve permitir ao usuário excluir um custo fixo.

### Lembretes por E-mail

- **P0** `CFIX-08` O sistema deve enviar um e-mail de lembrete consolidado ao usuário 3 dias antes da data de vencimento de cada custo fixo ativo.
- **P0** `CFIX-09` O sistema deve enviar um e-mail de lembrete consolidado ao usuário no dia do vencimento de cada custo fixo ativo.
- **P0** `CFIX-10` O e-mail de lembrete deve consolidar todos os custos fixos aplicáveis para o mesmo usuário e mesma data de lembrete em um único e-mail.
- **P0** `CFIX-11` O corpo do e-mail deve conter o nome de cada custo fixo e quantos dias faltam para o vencimento, sem exibir valores financeiros.
- **P0** `CFIX-12` O assunto do e-mail deve variar conforme o tipo de lembrete (antecedência vs. dia do vencimento).
- **P0** `CFIX-13` Quando o dia de vencimento não existir no mês corrente, o sistema deve considerar o último dia do mês como data de vencimento.
- **P0** `CFIX-14` O envio de lembrete deve ser idempotente, identificado pela combinação de usuário, data de referência do vencimento e tipo de lembrete.
- **P0** `CFIX-15` O sistema deve registrar internamente cada envio bem-sucedido.
- **P0** `CFIX-16` Falhas de envio devem ser registradas e permitir nova tentativa na próxima execução do job.
- **P0** `CFIX-17` Reexecuções do job devem pular silenciosamente lembretes já enviados com sucesso.
- **P0** `CFIX-18` O processamento de lembretes deve considerar o fuso horário `America/Sao_Paulo` para determinar a data corrente.
- **P0** `CFIX-19` Apenas custos fixos ativos devem participar do envio de lembretes.
- **P0** `CFIX-20` Usuários convidados de conta compartilhada não devem receber e-mails de custo fixo.

### Opt-out Global

- **P0** `CFIX-21` O sistema deve permitir ao usuário desativar todos os lembretes por e-mail de custos fixos de uma vez (opt-out global).
- **P0** `CFIX-22` Quando o opt-out global estiver ativo, nenhum e-mail de lembrete de custo fixo deve ser enviado ao usuário, independentemente do status individual de cada custo fixo.

### Estado Vazio

- **P1** `CFIX-23` Quando o usuário não tiver nenhum custo fixo cadastrado, a interface deve exibir uma mensagem orientando o cadastro.

## Constraints & Assumptions

### Constraints

- **Infraestrutura compartilhada com cold start**: A API pode pausar após ~15 minutos sem requisições; um job externo acorda a API diariamente a partir das 8h.
- **Envio idempotente obrigatório**: A chave de idempotência é composta por usuário + data de referência do vencimento + tipo de lembrete, para evitar duplicidade quando o job executar mais de uma vez.
- **Histórico de envio é controle interno**: Não há exposição do histórico de lembretes na interface do usuário.
- **Infraestrutura de e-mail existente**: O backend já possui `IProvedorEmail` e `ResendEmailProvedor`; o envio de lembretes deve usar essa infraestrutura.
- **MongoDB e padrão de repositories**: O backend usa MongoDB com repositories por entidade e mappings com índices criados na inicialização.
- **Performance e Carregamento de Dados**: O job de lembretes deve ser otimizado para evitar carga em memória (N+1 e excesso de dados) de custos fixos de usuários que tenham o opt-out global de e-mails ativo, resolvendo filtragens a nível de consulta.
- **Conteúdo mínimo no e-mail**: Nome do custo fixo e quantos dias faltam, sem valores financeiros.
- **Fuso horário fixo**: O processamento de lembretes usa `America/Sao_Paulo`.

### Assumptions

- **E-mail como canal único é suficiente**: O usuário checa e-mail regularmente e isso é suficiente para lembrar vencimentos. Se invalidada, outros canais (push, SMS) precisariam ser adicionados.
- **Provedor de e-mail disponível**: O Resend continuará disponível e funcional. Se invalidada, será necessário implementar fallback ou trocar de provedor.
- **Cold start não impede execução do job**: A infraestrutura compartilhada, mesmo com cold start, permite que o job diário execute dentro de um prazo razoável após ser acordado. Se invalidada, será necessário revisar a estratégia de hospedagem.

## Open Questions

| Question | Why It Matters | Owner | Status |
|----------|---------------|-------|--------|
| Qual será o mecanismo interno exato do job dentro da API? | Afeta confiabilidade, concorrência e observabilidade do disparo diário. | Time técnico | Open |
| Como o job externo autenticará ou protegerá a chamada que acorda a API? | Evita que terceiros disparem processamento indevido. | Time técnico | Open |
| Como o usuário cadastrará e gerenciará Custos Fixos na interface? | Afeta a experiência de cadastro, ativação/inativação e categoria opcional. | Produto / Frontend | Open |
| Qual retenção será aplicada ao histórico interno de envio? | Afeta crescimento da coleção e auditoria operacional. | Time técnico | Open |

## Additional Notes

- O conceito de **Custo Fixo** é separado de **Despesa**, **Despesa Recorrente** e **Despesa Parcelada**. O Custo Fixo é voltado exclusivamente a lembretes, sem impacto em cálculos financeiros.
- O CONTEXT.md com a análise completa do problema, prior art e achados no código está em `.specs/custos-fixos/CONTEXT.md`.
- Referências externas de validação: YNAB (transações agendadas como lembretes), billQ (lembretes de contas por e-mail), BillSnap (lembrete 3 dias antes do vencimento).
