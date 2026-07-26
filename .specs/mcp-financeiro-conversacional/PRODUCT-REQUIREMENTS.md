# MCP Financeiro Conversacional

## Overview

O MCP Financeiro Conversacional permite que titulares de contas individuais do FinanMap conectem qualquer agente compatível com o Model Context Protocol e utilizem a conversa como interface para sua vida financeira. A funcionalidade reduz o esforço de consultar, analisar e manter dados que hoje exigem navegação ou preenchimento manual no sistema. O agente poderá consultar informações autorizadas, preparar alterações e importar dados estruturados a partir de planilhas interpretadas no ambiente do próprio agente. O usuário continuará no controle de seus dados por meio de autenticação, isolamento por conta, prévias obrigatórias, confirmações explícitas e histórico auditável.

## Problem Statement

**Problem:** Usuários do FinanMap precisam navegar por diferentes telas, aplicar filtros, consolidar informações e preencher formulários para responder perguntas ou manter seus dados financeiros. Esse processo cria atrito para consultas recorrentes, análises comparativas, cadastros pontuais e migrações de planilhas, além de aumentar o risco de erro manual.

**Affected users:** Titulares de contas individuais do FinanMap que desejam consultar ou gerenciar suas próprias finanças por meio de um agente externo compatível com MCP. Isso inclui usuários em uso cotidiano, usuários migrando dados existentes e usuários que realizam análises mais avançadas de períodos ou categorias.

**Impact:** Sem uma interface conversacional segura, tarefas simples permanecem fragmentadas e migrações continuam trabalhosas. O produto perde uma oportunidade de reduzir abandono, acelerar a percepção de valor e diferenciar-se comercialmente por interoperabilidade com agentes de IA.

## Target Users

| User | Context | Primary Need |
|------|---------|--------------|
| Titular de conta individual em uso cotidiano | Consulta ou registra movimentações pontuais pelo agente de sua preferência | Obter respostas e manter dados sem navegar por múltiplas telas |
| Titular de conta individual em migração | Possui histórico em Excel ou CSV anexado ao agente | Importar dados válidos com revisão e correção orientada |
| Titular de conta individual em análise | Compara períodos, origens de receita ou categorias de gasto | Receber dados confiáveis para que o agente produza análises úteis |
| Titular de conta individual preocupado com controle e privacidade | Gerencia conexões externas e revisa atividades | Revogar acessos e compreender o que cada agente executou |

## Goals & Success Criteria

| Goal | Success Criterion | How to Measure |
|------|------------------|----------------|
| Permitir gestão financeira conversacional completa | Pelo menos 90% dos participantes do piloto concluem, sem abrir os formulários tradicionais, 8 de 10 cenários representativos de consulta, criação, alteração, exclusão e importação | Teste de usabilidade moderado com no mínimo 20 titulares de contas individuais |
| Preservar correção e confiabilidade financeira | 100% dos totais e comparações do conjunto de aceitação reconciliam com os registros de origem; pelo menos 99,5% das operações válidas do piloto terminam com resultado correto | Suíte de reconciliação financeira e telemetria das operações válidas do piloto |
| Impedir alterações não autorizadas ou acidentais | Nenhuma escrita ocorre sem confirmação explícita válida; 100% das operações MCP possuem registro de auditoria; nenhuma tentativa de acesso cruzado retorna dados | Testes automatizados de autorização, confirmação e auditoria, complementados por revisão de segurança antes do lançamento |
| Tornar importações corrigíveis e previsíveis | Um lote de 1.000 registros é processado com sucesso parcial; 100% dos itens rejeitados retornam referência de origem e motivo acionável; pelo menos 90% dos participantes conseguem corrigir e reenviar um item rejeitado | Testes de carga e sessão de usabilidade com planilhas Excel e CSV representativas |
| Garantir experiência responsiva e interoperável | 95% das consultas respondem em até 3 segundos; 95% das operações unitárias confirmadas respondem em até 5 segundos; a solução passa integralmente pela suíte de conformidade MCP adotada sem dependência específica de fornecedor | Monitoramento de latência em produção e execução da suíte de conformidade antes de cada lançamento |

## Scope

### In Scope

- Configuração, acompanhamento e revogação da conexão MCP pelo titular da conta.
- Dicas de utilização e prompts-base para os principais fluxos.
- Compatibilidade baseada no protocolo MCP, sem dependência de uma marca de agente.
- Consultas de categorias, receitas, despesas, investimentos e custos fixos.
- Consultas agregadas, comparações entre períodos e identificação de maiores impactos financeiros.
- Criação de categorias, receitas, despesas, investimentos e custos fixos.
- Alteração de categorias, receitas, despesas, investimentos e custos fixos.
- Exclusão definitiva de categorias, receitas, despesas, investimentos e custos fixos.
- Prévia obrigatória e confirmação explícita para qualquer operação de escrita.
- Importação de dados estruturados pelo agente a partir de Excel ou CSV.
- Validação item a item, sugestão de categorias e detecção de possíveis duplicidades.
- Importação dos itens válidos mesmo quando outros itens do lote falharem.
- Retorno visual e acionável dos itens que precisarem de correção.
- Auditoria de todas as operações realizadas via MCP.
- Histórico de operações visível ao próprio usuário.

### Non-Goals

- Contas compartilhadas, acessos delegados ou troca de contexto financeiro — a V1 atende apenas o titular de uma conta individual.
- Integrações específicas com ChatGPT, Claude, Gemini ou qualquer fornecedor — a interoperabilidade será definida pelo protocolo MCP.
- Chat ou modelo de IA hospedado pelo FinanMap — o raciocínio e a conversa pertencem ao agente escolhido pelo usuário.
- Armazenamento ou interpretação direta de arquivos Excel e CSV pelo FinanMap — o agente interpreta o documento e envia somente dados estruturados.
- Autorizações permanentes para escrita sem nova confirmação — cada alteração exige uma confirmação específica de uso único.
- Sincronização bancária, Open Finance ou conciliação automática — são integrações distintas da interface conversacional solicitada.
- Operações em contas de terceiros — o MCP só poderá acessar a conta individual autenticada.
- Metas financeiras ou outros domínios não listados neste PRD — a V1 concentra-se em categorias, receitas, despesas, investimentos e custos fixos.
- Garantia sobre a qualidade das recomendações produzidas por agentes externos — o FinanMap garante a qualidade dos dados e resultados das ferramentas, não o raciocínio do agente.
- Recuperação de registros excluídos — a exclusão confirmada será definitiva.

## Requirements

### Conexão e onboarding

- **P0** `MCP-01` O sistema deve permitir que o titular configure uma conexão MCP para sua conta individual.
- **P0** `MCP-02` O sistema deve informar se a conexão MCP está ativa, inválida ou revogada.
- **P0** `MCP-03` O sistema deve permitir que o titular revogue uma conexão ativa.
- **P0** `MCP-04` O sistema deve bloquear novas operações imediatamente após a revogação da conexão.
- **P0** `MCP-05` O sistema deve fornecer instruções suficientes para conectar qualquer agente compatível com MCP.
- **P0** `MCP-06` O sistema deve fornecer dicas de utilização da experiência conversacional.
- **P0** `MCP-07` O sistema deve fornecer prompts-base para consultas, análises, cadastros, alterações, exclusões e importações.
- **P0** `MCP-08` O sistema deve expor suas capacidades sem exigir comportamento exclusivo de um fornecedor de agente.

### Autenticação, autorização e privacidade

- **P0** `MCP-09` O sistema deve autenticar a identidade associada à conexão em toda chamada MCP.
- **P0** `MCP-10` O sistema deve limitar cada chamada aos dados da conta individual autenticada.
- **P0** `MCP-11` O sistema deve rejeitar identificadores de registros pertencentes a outra conta.
- **P0** `MCP-12` O sistema deve impedir que parâmetros enviados pelo agente substituam a identidade autenticada.
- **P0** `MCP-13` O sistema deve identificar cada ferramenta como leitura ou escrita.
- **P0** `MCP-14` O sistema deve disponibilizar somente os dados necessários para responder à consulta solicitada.
- **P0** `MCP-15` O sistema deve impedir a exposição de credenciais, segredos ou dados de autenticação nos resultados.
- **P0** `MCP-16` O sistema deve informar ao usuário que o agente externo processará os dados retornados conforme os termos do respectivo fornecedor.

### Consultas e dados para análise

- **P0** `MCP-17` O sistema deve permitir a consulta das categorias da conta.
- **P0** `MCP-18` O sistema deve permitir a consulta de receitas por período.
- **P0** `MCP-19` O sistema deve permitir a consulta de despesas por período.
- **P0** `MCP-20` O sistema deve permitir a consulta de investimentos por período.
- **P0** `MCP-21` O sistema deve permitir a consulta de custos fixos.
- **P0** `MCP-22` O sistema deve permitir filtros por categoria quando o tipo de registro possuir categoria.
- **P0** `MCP-23` O sistema deve permitir filtros por descrição quando o tipo de registro possuir descrição.
- **P0** `MCP-24` O sistema deve retornar totais financeiros para o período solicitado.
- **P0** `MCP-25` O sistema deve retornar as maiores receitas ou despesas conforme quantidade solicitada.
- **P0** `MCP-26` O sistema deve retornar valores agrupados por categoria.
- **P0** `MCP-27` O sistema deve retornar dados suficientes para comparação entre dois períodos.
- **P0** `MCP-28` O sistema deve explicitar o período aplicado em cada resposta financeira.
- **P0** `MCP-29` O sistema deve explicitar a moeda aplicada em cada resposta financeira.
- **P0** `MCP-30` O sistema deve explicitar os filtros aplicados em cada resposta financeira.
- **P0** `MCP-31` O sistema deve retornar estado vazio inequívoco quando não houver registros correspondentes.
- **P0** `MCP-32` O sistema deve limitar respostas extensas sem omitir silenciosamente a existência de resultados adicionais.
- **P1** `MCP-33` O sistema deve permitir que o agente continue uma consulta extensa a partir do ponto indicado na resposta anterior.

### Preparação e confirmação de escritas

- **P0** `MCP-34` O sistema deve preparar uma prévia antes de qualquer criação.
- **P0** `MCP-35` O sistema deve preparar uma prévia antes de qualquer alteração.
- **P0** `MCP-36` O sistema deve preparar uma prévia antes de qualquer exclusão.
- **P0** `MCP-37` O sistema deve manter os dados inalterados durante a preparação da prévia.
- **P0** `MCP-38` O sistema deve identificar o tipo de ação proposta na prévia.
- **P0** `MCP-39` O sistema deve identificar cada registro afetado na prévia.
- **P0** `MCP-40` O sistema deve apresentar os valores atuais relevantes em uma prévia de alteração.
- **P0** `MCP-41` O sistema deve apresentar os valores propostos em uma prévia de criação ou alteração.
- **P0** `MCP-42` O sistema deve destacar a irreversibilidade em uma prévia de exclusão.
- **P0** `MCP-43` O sistema deve exigir confirmação explícita vinculada à prévia apresentada.
- **P0** `MCP-44` O sistema deve aceitar cada confirmação uma única vez.
- **P0** `MCP-45` O sistema deve rejeitar confirmações genéricas que não identifiquem uma prévia específica.
- **P0** `MCP-46` O sistema deve rejeitar uma confirmação quando o registro tiver mudado desde a prévia.
- **P0** `MCP-47` O sistema deve executar somente os itens descritos na prévia confirmada.
- **P0** `MCP-48` O sistema deve retornar o resultado individual da operação confirmada.
- **P0** `MCP-49` O sistema deve impedir duplicação quando uma solicitação confirmada for repetida.

### Gestão dos domínios financeiros

- **P0** `MCP-50` O sistema deve permitir criar categorias.
- **P0** `MCP-51` O sistema deve permitir alterar categorias.
- **P0** `MCP-52` O sistema deve permitir excluir categorias definitivamente.
- **P0** `MCP-53` O sistema deve permitir criar receitas.
- **P0** `MCP-54` O sistema deve permitir alterar receitas.
- **P0** `MCP-55` O sistema deve permitir excluir receitas definitivamente.
- **P0** `MCP-56` O sistema deve permitir criar despesas.
- **P0** `MCP-57` O sistema deve permitir alterar despesas.
- **P0** `MCP-58` O sistema deve permitir excluir despesas definitivamente.
- **P0** `MCP-59` O sistema deve permitir criar investimentos.
- **P0** `MCP-60` O sistema deve permitir alterar investimentos.
- **P0** `MCP-61` O sistema deve permitir excluir investimentos definitivamente.
- **P0** `MCP-62` O sistema deve permitir criar custos fixos mensais.
- **P0** `MCP-63` O sistema deve permitir alterar custos fixos mensais.
- **P0** `MCP-64` O sistema deve permitir excluir custos fixos mensais definitivamente.
- **P0** `MCP-65` O sistema deve validar os campos obrigatórios conforme o tipo financeiro.
- **P0** `MCP-66` O sistema deve rejeitar valores, datas ou recorrências incompatíveis com as regras do tipo financeiro.
- **P0** `MCP-67` O sistema deve exigir esclarecimento quando uma solicitação de escrita possuir dados ambíguos.
- **P0** `MCP-68` O sistema deve retornar o campo que precisa ser esclarecido.
- **P0** `MCP-69` O sistema deve respeitar as regras existentes de relacionamento entre categorias e registros financeiros.
- **P0** `MCP-70` O sistema deve impedir uma exclusão que viole uma regra obrigatória do domínio.
- **P0** `MCP-71` O sistema deve explicar ao usuário por que uma exclusão foi impedida.

### Importação de dados estruturados

- **P0** `MCP-72` O sistema deve receber dados estruturados preparados pelo agente a partir de Excel ou CSV.
- **P0** `MCP-73` O sistema deve permitir que um lote contenha categorias, receitas, despesas, investimentos e custos fixos.
- **P0** `MCP-74` O sistema deve preservar uma referência à linha, aba ou item de origem fornecida pelo agente.
- **P0** `MCP-75` O sistema deve validar cada item do lote independentemente.
- **P0** `MCP-76` O sistema deve classificar cada item como válido, inválido, pendente ou possível duplicidade.
- **P0** `MCP-77` O sistema deve relacionar categorias informadas pelo agente às categorias existentes quando houver correspondência inequívoca.
- **P0** `MCP-78` O sistema deve sugerir categorias existentes quando a correspondência não for inequívoca.
- **P0** `MCP-79` O sistema deve incluir a criação de novas categorias na prévia quando ela for necessária para o lote.
- **P0** `MCP-80` O sistema deve informar o motivo de cada item inválido ou pendente.
- **P0** `MCP-81` O sistema deve orientar a correção de cada item inválido ou pendente em linguagem compreensível.
- **P0** `MCP-82` O sistema deve bloquear uma possível duplicidade até uma decisão explícita do usuário.
- **P0** `MCP-83` O sistema deve permitir que o usuário ignore uma possível duplicidade.
- **P0** `MCP-84` O sistema deve permitir que o usuário autorize explicitamente a importação de uma possível duplicidade.
- **P0** `MCP-85` O sistema deve apresentar uma prévia com totais por tipo financeiro.
- **P0** `MCP-86` O sistema deve apresentar uma prévia com contagens por estado de validação.
- **P0** `MCP-87` O sistema deve apresentar os valores financeiros totais propostos na importação.
- **P0** `MCP-88` O sistema deve exigir confirmação explícita de uso único para o lote.
- **P0** `MCP-89` O sistema deve importar os itens válidos do lote confirmado.
- **P0** `MCP-90` O sistema deve manter sem gravação os itens inválidos ou pendentes.
- **P0** `MCP-91` O sistema deve retornar o resultado de cada item após a execução.
- **P0** `MCP-92` O sistema deve retornar o motivo da falha de cada item não importado.
- **P0** `MCP-93` O sistema deve permitir o reenvio apenas dos itens corrigidos.
- **P0** `MCP-94` O sistema deve impedir que o reenvio duplique itens já importados pelo mesmo lote.
- **P0** `MCP-95` O sistema deve suportar pelo menos 1.000 itens em uma única importação.
- **P0** `MCP-96` O sistema não deve receber o conteúdo binário do documento original.
- **P0** `MCP-97` O sistema não deve armazenar Excel, CSV ou qualquer outro documento original.

### Auditoria e histórico

- **P0** `MCP-98` O sistema deve registrar todas as operações executadas via MCP.
- **P0** `MCP-99` O sistema deve registrar o usuário responsável pela conexão.
- **P0** `MCP-100` O sistema deve registrar a ferramenta utilizada.
- **P0** `MCP-101` O sistema deve registrar os parâmetros relevantes enviados à ferramenta.
- **P0** `MCP-102` O sistema deve registrar a data e hora da operação.
- **P0** `MCP-103` O sistema deve registrar o resultado da operação.
- **P0** `MCP-104` O sistema deve registrar a origem da solicitação.
- **P0** `MCP-105` O sistema deve omitir credenciais e segredos dos parâmetros auditados.
- **P0** `MCP-106` O sistema deve preservar a auditoria de um registro após sua exclusão definitiva.
- **P0** `MCP-107` O sistema deve permitir que o titular consulte seu próprio histórico MCP.
- **P0** `MCP-108` O sistema deve mostrar ao titular a ação realizada.
- **P0** `MCP-109` O sistema deve mostrar ao titular a data da operação.
- **P0** `MCP-110` O sistema deve mostrar ao titular o status da operação.
- **P0** `MCP-111` O sistema deve mostrar ao titular um resumo do resultado.
- **P1** `MCP-112` O sistema deve permitir que o titular filtre o histórico por período.
- **P1** `MCP-113` O sistema deve permitir que o titular filtre o histórico por tipo de operação.
- **P0** `MCP-114` O sistema deve impedir que o histórico de uma conta seja acessado por outra conta.

### Falhas, limites e previsibilidade

- **P0** `MCP-115` O sistema deve retornar erros estruturados que permitam ao agente explicar o problema ao usuário.
- **P0** `MCP-116` O sistema deve distinguir falhas de autenticação, autorização, validação, conflito e indisponibilidade.
- **P0** `MCP-117` O sistema deve evitar revelar a existência de dados de outra conta em mensagens de erro.
- **P0** `MCP-118` O sistema deve informar quando uma solicitação exceder um limite operacional.
- **P0** `MCP-119` O sistema deve orientar como reduzir ou dividir uma solicitação que excedeu o limite.
- **P0** `MCP-120` O sistema deve informar quando uma capacidade solicitada não estiver disponível.
- **P0** `MCP-121` O sistema deve executar ferramentas de leitura sem exigir confirmação.
- **P0** `MCP-122` O sistema deve manter a operação não executada quando a confirmação for ausente, inválida ou expirada.
- **P0** `MCP-123` O sistema deve informar quando o resultado de uma escrita for desconhecido por falha de comunicação.
- **P0** `MCP-124` O sistema deve permitir a verificação segura do resultado antes de uma nova tentativa.
- **P1** `MCP-125` O sistema deve apresentar ao titular indisponibilidades recentes que tenham afetado operações confirmadas.

## Constraints & Assumptions

### Constraints

- **Interoperabilidade:** a solução deve seguir o protocolo MCP sem exigir recursos exclusivos de um agente específico.
- **Conta individual:** a V1 não pode acessar contas compartilhadas, delegadas ou pertencentes a terceiros.
- **Isolamento:** toda operação deve permanecer limitada à conta autenticada.
- **Controle humano:** nenhuma escrita pode ocorrer sem prévia e confirmação explícita de uso único.
- **Exclusão definitiva:** uma exclusão confirmada não poderá ser restaurada.
- **Minimização de documentos:** o FinanMap não pode receber nem armazenar o arquivo original usado pelo agente.
- **Auditoria:** toda operação MCP deve ser rastreável sem registrar credenciais.
- **Privacidade:** o tratamento dos dados deve respeitar a legislação de proteção de dados aplicável e as permissões da conta.
- **Consistência financeira:** resultados MCP devem obedecer às mesmas regras de negócio aplicadas às operações realizadas diretamente no FinanMap.

### Assumptions

- **Agente com suporte adequado:** o agente escolhido pelo usuário consegue conectar-se a servidores MCP e apresentar solicitações de confirmação.
- **Interpretação externa de documentos:** Excel e CSV são interpretados no ambiente do agente antes do envio de dados estruturados.
- **Responsabilidade da conversa:** o agente é responsável por interpretar linguagem natural e formular a resposta final ao usuário.
- **Dados identificáveis:** os registros financeiros possuem identificadores estáveis suficientes para alteração, exclusão e prevenção de repetição.
- **Volume representativo:** lotes de até 1.000 itens cobrem a maior parte das migrações esperadas na V1; se isso se provar falso, limites e processamento deverão ser revistos.
- **Campos do domínio disponíveis:** categorias, receitas, despesas, investimentos e custos fixos possuem os dados necessários para atender aos exemplos deste PRD; se isso se provar falso, o domínio do produto deverá ser ampliado.

## Open Questions

| Question | Why It Matters | Owner | Status |
|----------|---------------|-------|--------|
| Por quanto tempo os registros detalhados de auditoria devem ser mantidos? | Define a política de retenção, privacidade e capacidade de suporte | Produto e Privacidade | Open |
| Qual será o limite de produção acima do mínimo aceito de 1.000 itens por importação? | Afeta experiência para migrações extensas e proteção operacional | Produto e Engenharia | Resolved for V1 — limite fixado em 1.000; aumento fica para V2+ |
| Qual mecanismo de autenticação e revogação atenderá melhor aos diferentes clientes MCP? | Afeta segurança, onboarding e interoperabilidade sem mudar os requisitos de produto | Arquitetura e Segurança | Resolved — OAuth 2.1 Authorization Code + PKCE, discovery, scopes e revogação imediata |
| Qual aviso ou consentimento deve explicar o processamento de dados pelo fornecedor do agente? | Afeta transparência ao usuário e conformidade de privacidade | Produto, Jurídico e Privacidade | Open |
| Qual será a janela de validade de uma prévia antes de exigir nova confirmação? | Afeta segurança contra dados desatualizados e fluidez da conversa | Produto e Segurança | Resolved — 15 minutos configuráveis |

## Additional Notes

### Fluxo de referência

1. O titular configura uma conexão MCP no FinanMap.
2. O agente descobre as ferramentas de leitura e escrita disponíveis.
3. Consultas são executadas diretamente no escopo da conta autenticada.
4. Escritas geram uma prévia sem alterar dados.
5. O agente apresenta a prévia ao usuário.
6. O usuário confirma explicitamente a ação específica.
7. O sistema valida novamente o contexto antes de executar.
8. O sistema executa a operação confirmada.
9. O agente apresenta o resultado ao usuário.
10. O usuário pode consultar o evento no histórico MCP do FinanMap.

### Fluxo de referência para importação

1. O usuário anexa Excel ou CSV ao agente.
2. O agente interpreta colunas, linhas e tipos financeiros.
3. O agente solicita esclarecimentos quando necessário.
4. O agente envia somente dados estruturados ao FinanMap.
5. O FinanMap valida os itens e devolve uma prévia.
6. Possíveis duplicidades ficam bloqueadas até decisão explícita.
7. O usuário confirma o lote revisado.
8. Itens válidos são importados.
9. Itens que falharem retornam com motivo e referência de origem.
10. O usuário corrige e reenvia somente os itens necessários.

### Prioritization rationale

- Os requisitos P0 representam o fluxo seguro mínimo prometido ao cliente: conexão, leitura, gestão completa dos cinco domínios, importação, confirmação e auditoria.
- Os requisitos P1 melhoram a operação recorrente, mas não impedem a conclusão segura dos cenários principais no primeiro lançamento.
- Não há requisitos P2 nesta versão, pois extensões não essenciais foram mantidas como non-goals em vez de ampliar silenciosamente o escopo.

### Failure conditions

O produto deve ser considerado incorretamente projetado ou implementado se qualquer uma destas condições ocorrer:

- Uma operação de escrita for executada sem confirmação explícita vinculada a uma prévia.
- Uma conexão acessar dados pertencentes a outra conta.
- Uma exclusão ocorrer sem aviso claro de irreversibilidade.
- Uma repetição de confirmação criar registros duplicados.
- Um documento original for recebido ou armazenado pelo FinanMap.
- Uma importação ocultar itens que falharam ou não explicar o motivo da falha.
- Uma possível duplicidade for importada sem decisão explícita do usuário.
- Os totais retornados não puderem ser reconciliados com os dados da conta.
- Uma operação MCP deixar de produzir registro de auditoria.
- O fluxo depender de recursos exclusivos de uma marca de agente.
- Menos de 90% dos participantes do piloto concluírem os cenários principais definidos nos critérios de sucesso.
