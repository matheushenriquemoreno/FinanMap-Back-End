# Contexto - Custos Fixos

| Campo | Valor |
|-------|-------|
| Autor | Codex com o usuario |
| Data | 2026-05-21 |

## Enquadramento do Problema

**O que acontece hoje?** O FinanMap controla rendimentos, despesas e investimentos mensais, mas nao possui um lugar proprio para acompanhar compromissos fixos por dia de vencimento. Fora do FinanMap, cada pessoa pode usar memoria, planilha, caderno, calendario ou outro controle proprio.

**Quem e afetado?** O usuario proprietario dos dados financeiros, que precisa acompanhar compromissos como agua, energia, internet e outros vencimentos mensais.

**Custo do status quo.** O usuario depende de controles externos ou memoria para lembrar vencimentos fixos. Isso aumenta o risco de esquecer compromissos, principalmente quando o valor ainda nao esta lancado como despesa mensal.

## Stakeholders e Usuarios

| Stakeholder / Usuario | Papel ou Contexto | Dor ou Necessidade |
|-----------------------|-------------------|--------------------|
| Usuario proprietario | Dono dos dados financeiros e do e-mail da conta atual | Acompanhar compromissos fixos e receber lembretes proximos ao vencimento |

## Restricoes

### Tecnicas
- A API roda em infraestrutura compartilhada que pode pausar apos cerca de 15 minutos sem requisicoes.
- Um job externo acordara a API diariamente a partir das 8h, chamando a propria aplicacao.
- O fluxo de envio precisa ser idempotente para evitar duplicidade quando a chamada externa ou o job interno executarem mais de uma vez.
- A idempotencia foi definida por usuario, data de referencia do vencimento e tipo de lembrete.
- O historico de envio e controle interno, sem exposicao em tela para o usuario.
- O backend ja possui infraestrutura de envio de e-mail baseada em `IProvedorEmail` e `ResendEmailProvedor`.
- O backend usa MongoDB, repositories por entidade e mappings com indices criados na inicializacao.

### Legais / Compliance
- Nenhuma restricao especifica foi identificada.
- O conteudo do e-mail deve ser minimo: nome do custo fixo e quantos dias faltam, sem valores financeiros.

### Operacionais
- O lembrete por e-mail considera o fuso `America/Sao_Paulo`.
- O envio ocorre para o e-mail atual do usuario proprietario dos dados.
- Usuarios convidados de conta compartilhada nao recebem e-mail de custo fixo.
- Quando o dia de vencimento nao existir no mes, considera-se o ultimo dia do mes.
- O envio bem-sucedido deve ser registrado; falhas permitem nova tentativa.
- Reexecucoes devem pular silenciosamente lembretes ja enviados com sucesso.

### Tempo / Time
- Nenhuma restricao de prazo ou equipe foi identificada.

## Entendimento Confirmado

- **Custo Fixo** e separado de **Despesa**, **Despesa Recorrente** e **Despesa Parcelada**.
- O Custo Fixo e voltado a lembrete, nao a previsao financeira.
- Custo Fixo nao possui valor.
- Custo Fixo possui dia de vencimento, nao data completa.
- Custo Fixo pode ser ativado ou inativado.
- Apenas custos fixos ativos participam do envio de lembretes.
- Categoria de despesa e opcional.
- Transformar um lembrete de Custo Fixo em Despesa e uma melhoria futura, nao parte do entendimento atual.
- Todo Custo Fixo ativo gera lembrete por e-mail.
- O lembrete por e-mail ocorre 3 dias antes e no dia do vencimento.
- O e-mail e consolidado por usuario e por dia de lembrete, listando todos os custos fixos aplicaveis.
- O assunto do e-mail pode variar conforme o tipo de lembrete.
- O corpo do e-mail deve seguir um formato padrao, contendo nome do custo fixo e quantos dias faltam.
- Nao deve haver duplicidade de Custo Fixo ativo para o mesmo usuario considerando nome e dia de vencimento.

## Prior Art

| Solucao / Abordagem | Fonte | Achado Principal | Aplicabilidade |
|---------------------|-------|------------------|----------------|
| Recados financeiros por transacoes agendadas | YNAB | Transacoes agendadas podem funcionar como lembretes financeiros, inclusive sem depender de trocar para outro app. | Media - reforca a ligacao entre financas e lembretes, mas mistura lembrete com transacao. |
| Lembretes de contas por e-mail/SMS | billQ | Produto dedicado a lembrar contas antes do vencimento por e-mail ou SMS. | Alta - valida e-mail como canal direto para vencimentos. |
| Lembrete 3 dias antes | BillSnap | Produto de bills tracking destaca lembrete 3 dias antes do vencimento e cadastro de contas recorrentes. | Alta - coincide com a regra de antecedencia definida pelo usuario. |

## Achados no Codigo

- **`CONTEXT.md`**: O glossario do backend define Custo Fixo como compromisso financeiro recorrente para lembrete mensal, sem valor e separado de Despesa.
- **`Domain/Despesa/Entity/Despesa.cs`**: Despesa ja possui campos para lote, parcelamento e recorrencia (`DespesaOrigemId`, `IsParcelado`, `IsRecorrente`, `ParcelaAtual`, `TotalParcelas`), mas nao representa cadastro permanente de custo fixo.
- **`Application/Despesa/Services/DespesaService.cs`**: Existe fluxo para lancar despesas em lote, atualizar/excluir lotes e recalcular agrupadoras, relevante por ser o conceito que nao deve ser confundido com Custo Fixo.
- **`Application/Email/Services/EmailService.cs`**: O envio de e-mail hoje atende login, usando HTML especifico para codigo de acesso.
- **`Application/Email/Interfaces/IProvedorEmail.cs`**: A aplicacao ja abstrai o provedor de e-mail com `EnviarEmail`.
- **`Infra.data/Email/ResendEmailProvedor.cs`**: A infraestrutura atual envia e-mails via Resend e registra logs de sucesso ou erro.
- **`Infra.data/MediaTrConfigure/Publisher/ChannelPublisherWorker.cs`**: Ja existe um `BackgroundService`, mas voltado ao processamento de notificacoes internas do MediatR, nao a agendamento recorrente diario.
- **`Infra.data/Mongo/RepositoryBase/RepositoryMongoBase.cs`**: Repositories usam colecoes Mongo por entidade, com operacoes genericas de add, update, delete e consultas.
- **`Infra.data/Mongo/Mappings/*`**: Mappings criam indices em colecoes como Categoria, CodigoLogin, RefreshToken e Transacoes, o que indica um padrao existente para mapear novas entidades e indices.
- **`Domain/Login/Interfaces/IUsuarioLogado.cs` e servicos existentes**: Os fluxos atuais distinguem usuario logado, contexto de dados e permissao em modo compartilhado.

## Referencias Externas

| Referencia | URL | Achado Principal |
|------------|-----|------------------|
| YNAB - How to Turn Your Budget into a To-Do List | https://www.ynab.com/blog/turn-your-budget-into-a-to-do-list | Mostra o uso de transacoes agendadas como lembretes financeiros e tarefas com data. |
| billQ | https://www.mybillq.com/ | Produto focado em lembrar contas antes do vencimento por texto ou e-mail. |
| BillSnap | https://billsnap.online/ | Produto de acompanhamento de contas com lembretes 3 dias antes, recorrencia e foco em nao perder vencimentos. |

## Perguntas Abertas

| Pergunta | Por que Importa | Dono | Status |
|----------|-----------------|------|--------|
| Qual sera o mecanismo interno exato do job dentro da API? | Afeta confiabilidade, concorrencia e observabilidade do disparo diario. | Time tecnico | Aberta |
| Como o job externo autenticara ou protegera a chamada que acorda a API? | Evita que terceiros disparem processamento indevido. | Time tecnico | Aberta |
| Como o usuario cadastrara e gerenciara Custos Fixos na interface? | Afeta a experiencia de cadastro, ativacao/inativacao e categoria opcional. | Produto / Frontend | Aberta |
| Qual retencao sera aplicada ao historico interno de envio? | Afeta crescimento da colecao e auditoria operacional. | Time tecnico | Aberta |
