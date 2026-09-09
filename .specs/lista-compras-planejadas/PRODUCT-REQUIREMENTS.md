# Lista de Compras Planejadas — Back-end

| Status       | Aprovado    |
|--------------|-------------|
| Created      | 2026-08-29  |
| Last Updated | 2026-09-09  |

## Histórico de atualizações

| Data       | Alteração |
|------------|-----------|
| 2026-08-29 | Aprovação do PRD unificado da Lista de Compras Planejadas. |
| 2026-09-09 | Separação editorial das responsabilidades de back-end, sem alteração do escopo ou do comportamento de produto aprovado. |
| 2026-09-09 | Regra de exclusão de item comprado confirmada: a despesa vinculada permanece e somente o vínculo e o item são removidos (`LCP-BE-14`). |

## Visão geral

O back-end da Lista de Compras Planejadas mantém os itens que o usuário deseja comprar, seus valores estimados, prioridades, descrições e links de lojas. Ele sustenta a transição de um item pendente para comprado, preserva a estimativa original, registra o valor real e permite vincular uma despesa comum do FinanMap. Também aplica as regras de domínio e as permissões já existentes para contas compartilhadas.

Este documento delimita somente os comportamentos observáveis sob responsabilidade do back-end. Navegação, formulários, mensagens e apresentação dos dados estão no PRD de front-end da mesma iniciativa.

## Problema e impacto

**Problema:** o FinanMap não possui um recurso de dados e regras para manter compras futuras planejadas e conectá-las, após a conclusão, ao fluxo financeiro mensal.

**Quem é afetado:** proprietários de contas e convidados autorizados que precisam consultar ou alterar o planejamento financeiro compartilhado.

**Impacto se não for resolvido:** a interface não consegue manter uma fonte confiável para o planejamento, calcular totais atuais, preservar o histórico de compras nem garantir autorização e consistência nos fluxos de conclusão e reversão.

## Usuários e perfis afetados

| Perfil | Contexto de uso | Necessidade principal |
|--------|-----------------|----------------------|
| Proprietário da conta | Mantém seus dados financeiros | Consultar e alterar sua lista com isolamento dos dados de outros usuários. |
| Convidado com permissão de edição | Atua no contexto compartilhado de um proprietário | Consultar e alterar a lista autorizada. |
| Convidado com permissão de visualização | Consulta o contexto compartilhado | Obter os dados sem conseguir alterá-los. |

## Objetivos e critérios de sucesso

| Objetivo | Critério de sucesso | Forma de verificação |
|----------|--------------------|----------------------|
| Manter o ciclo de vida da compra planejada | Operações de cadastro, alteração, conclusão, reversão e exclusão produzem o estado esperado. | Executar testes de integração para cada transição. |
| Fornecer totais confiáveis | Totais de pendentes e comprados correspondem aos registros atuais do proprietário consultado. | Comparar respostas com valores conhecidos em testes. |
| Integrar a compra ao controle mensal | Uma despesa opcional pode ser criada e vinculada ao item comprado. | Concluir uma compra com despesa e consultar ambos os registros. |
| Proteger o contexto compartilhado | Apenas o proprietário e convidados com edição realizam escrita. | Testar leitura e escrita com cada nível de acesso. |

## Escopo e não objetivos

### Dentro do escopo

- Cadastro, consulta, edição e exclusão de itens pendentes.
- Armazenamento de múltiplos links de loja por item.
- Total estimado e ordenação da lista de pendentes.
- Conclusão de compra com valor real e data.
- Consulta do histórico de itens comprados.
- Criação e vínculo opcional de uma despesa comum ao item comprado.
- Reversão de compra, com opção de excluir a despesa vinculada.
- Exclusão de itens comprados.
- Comparativo agregado de valores estimados e reais.
- Isolamento por proprietário e autorização em contextos compartilhados.

### Fora do escopo

- Telas, navegação, formulários e mensagens — são responsabilidades do front-end.
- Notificações automáticas sobre a lista — a primeira versão pressupõe consulta ativa.
- Teto de orçamento configurável — a versão cobre estimativa e total, sem limite definido pelo usuário.
- Consulta ou comparação automática de preços das lojas — os links são referências, não cotações.
- Categoria e data desejada no item planejado — não integram o cadastro desta versão.
- Aportes por item ou conversão em Meta Financeira — esse comportamento pertence ao módulo de metas e não foi solicitado.
- Edição direta de um item já comprado — não existe requisito aprovado para essa ação; eventual inclusão exige revisão do PRD.

### Adiado

- Nenhum.

## Requisitos funcionais

Prioridades: **Essencial** (bloqueia a entrega), **Importante** (deve entrar), **Desejável** (entra se couber, sem comprometer os demais).

- **Essencial** `LCP-BE-01` O serviço deve permitir cadastrar um item pendente com nome, valor estimado, prioridade, descrição e links de lojas.
- **Essencial** `LCP-BE-02` O serviço deve permitir alterar todos os campos de um item pendente.
- **Essencial** `LCP-BE-03` O serviço deve permitir excluir um item pendente.
- **Essencial** `LCP-BE-04` O serviço deve manter múltiplos links contendo URL e nome da loja em um item.
- **Essencial** `LCP-BE-05` A consulta de pendentes deve informar o total correspondente à soma de seus valores estimados.
- **Essencial** `LCP-BE-06` A consulta de pendentes deve retornar os itens na ordem Alta, Média e Baixa e, dentro da mesma prioridade, do mais recente para o mais antigo.
- **Essencial** `LCP-BE-07` O serviço deve permitir marcar um item pendente como comprado mediante valor real e data da compra válidos.
- **Essencial** `LCP-BE-08` Um item concluído deve deixar de integrar os pendentes e passar a integrar os comprados.
- **Essencial** `LCP-BE-09` O serviço deve permitir criar uma despesa comum vinculada ao item comprado, contendo mês, categoria e o valor real pago.
- **Essencial** `LCP-BE-10` Cada item comprado deve admitir no máximo uma despesa vinculada.
- **Essencial** `LCP-BE-11` A consulta de comprados deve informar, por item, a estimativa original, o valor real e a data da compra.
- **Essencial** `LCP-BE-12` O serviço deve permitir reverter uma compra, descartando valor real e data e restaurando o item pendente com sua estimativa original.
- **Essencial** `LCP-BE-13` Ao reverter uma compra, o serviço deve permitir excluir a despesa vinculada quando essa opção for solicitada.
- **Essencial** `LCP-BE-14` O serviço deve excluir um item comprado removendo seu vínculo sem excluir a despesa vinculada existente.
- **Essencial** `LCP-BE-15` Operações de escrita devem ser autorizadas somente ao proprietário dos dados e a convidados com permissão de edição.
- **Essencial** `LCP-BE-16` Consultas devem permitir o acesso de convidados com permissão de visualização ao contexto compartilhado autorizado.
- **Importante** `LCP-BE-17` A consulta de comprados deve informar o total estimado e o total efetivamente gasto dos itens comprados.

## Expectativas não funcionais

- **EXPECT-BE-01** Valores monetários retornados e mantidos pelo serviço devem conservar precisão compatível com a exibição em Real (BRL).
- **EXPECT-BE-02** As transições de conclusão e reversão devem produzir um estado consistente, sem o mesmo item simultaneamente pendente e comprado.
- **EXPECT-BE-03** As consultas devem permanecer utilizáveis com centenas de itens acumulados por proprietário.
- **EXPECT-BE-04** Falhas de validação, autorização ou operação devem ser distinguíveis pelo consumidor do serviço.
- **EXPECT-BE-05** Uma falha não deve confirmar uma transição ou vínculo que não tenha sido efetivamente concluído.

## Regras de negócio e restrições

### Regras de negócio

- Nome, valor estimado e prioridade são obrigatórios; descrição e links são opcionais.
- A prioridade aceita somente Alta, Média e Baixa.
- Valor estimado e valor real devem ser maiores que zero.
- Cada link deve conter URL válida e nome da loja.
- A data da compra pode ser igual ou anterior à data atual.
- Ao concluir a compra, o valor real passa a representar o valor pago e a estimativa original é preservada para comparação.
- A despesa vinculada utiliza o valor real da compra.
- Cada item comprado pode ter no máximo uma despesa vinculada.
- Ao reverter, valor real e data são descartados e a estimativa original é restaurada.
- Ao excluir um item comprado, a despesa vinculada permanece e somente o vínculo e o item são removidos.

### Restrições

- Os dados pertencem ao proprietário e devem utilizar o mecanismo de compartilhamento já existente, sem permissões específicas para a lista.
- A despesa vinculada é uma despesa comum do módulo mensal, com mês, ano, categoria e valor; não haverá tipo especial de despesa.
- As operações devem respeitar o contexto do proprietário solicitado e nunca expor dados de outro contexto não autorizado.

## Premissas

- O mecanismo de compartilhamento existente fornece a identidade do proprietário e o nível de permissão aplicável — risco: divergências nesse contrato podem conceder ou bloquear acesso indevidamente.
- Dentro da mesma prioridade, itens mais recentes aparecem primeiro — risco: baixo; outra preferência exigiria revisão da ordenação aprovada.
- O histórico de comprados permanece até exclusão manual — risco: o volume armazenado cresce indefinidamente.
- Não há limite rígido de itens ou links nesta versão — risco: volumes muito altos podem exigir paginação ou limites em evolução futura.
- O módulo mensal aceita a criação da despesa necessária ao vínculo — risco: incompatibilidade contratual impediria fechar o fluxo de compra com despesa.

## Fluxos e casos de borda

### Fluxos principais

- **Planejar compra:** uma solicitação válida cria um pendente no contexto do proprietário; consultas seguintes incluem o item e o novo total estimado.
- **Concluir compra:** uma solicitação válida registra valor real e data, move o item para comprados e, quando solicitado, cria e vincula uma despesa comum.
- **Desfazer compra:** uma solicitação de reversão restaura o item pendente; quando solicitada, também exclui a despesa vinculada.

### Estados vazios

- Consultas sem pendentes retornam coleção vazia e total estimado igual a zero.
- Consultas sem comprados retornam coleção vazia e totais comparativos iguais a zero.

### Erros e falhas

- Dados inválidos são rejeitados sem alterar o estado do item.
- Escrita por usuário sem permissão é rejeitada sem revelar dados além do necessário.
- Operação sobre item inexistente ou fora do contexto autorizado não altera outros registros.
- Solicitação de uma segunda despesa para o mesmo item é rejeitada.

### Limites

- Não há limite rígido de itens ou links nesta versão.
- Totais devem refletir o estado atual dos registros do proprietário consultado.

## Critérios de aceitação

1. Criar e consultar um item com descrição e dois links preserva todos os dados informados.
2. Adicionar, alterar ou excluir um pendente produz o total estimado correto na consulta seguinte.
3. A consulta de pendentes respeita prioridade e data de criação conforme a ordenação aprovada.
4. Concluir uma compra retira o item dos pendentes e o inclui nos comprados com estimativa original, valor real e data.
5. Concluir uma compra com registro de despesa cria uma despesa comum vinculada ao item e impede um segundo vínculo.
6. Reverter uma compra restaura a estimativa original; quando a exclusão da despesa é solicitada, o vínculo e a despesa deixam de existir.
7. Excluir um comprado com despesa vinculada remove o item e o vínculo, preserva a despesa e não altera os demais itens.
8. O proprietário e o convidado com edição conseguem escrever; o convidado com visualização consegue apenas consultar.
9. A consulta de comprados retorna totais estimado e real correspondentes aos itens atuais.
10. Valores, datas, prioridades e links inválidos são rejeitados sem mudança parcial de estado.

## Rastreabilidade com o PRD unificado

| Responsabilidade de back-end | Requisitos de produto de origem |
|------------------------------|---------------------------------|
| `LCP-BE-01` a `LCP-BE-04` | `LCP-01` a `LCP-04` |
| `LCP-BE-05` e `LCP-BE-06` | `LCP-05` e `LCP-06` |
| `LCP-BE-07` a `LCP-BE-10` | `LCP-07` a `LCP-09` e regra de vínculo único |
| `LCP-BE-11` a `LCP-BE-14` | `LCP-10` a `LCP-13` |
| `LCP-BE-15` e `LCP-BE-16` | `LCP-14` |
| `LCP-BE-17` | `LCP-15` |
| `EXPECT-BE-01` a `EXPECT-BE-03` | `EXPECT-01` e `EXPECT-03`, aplicadas ao serviço |

## Perguntas em aberto

| Pergunta | Por que importa | Status |
|----------|-----------------|--------|
| Um item já comprado poderá ser editado diretamente em uma versão futura? | Uma resposta positiva adicionará regras de correção do valor real, da data e possivelmente da despesa vinculada. | Aberta, não bloqueante para esta versão |
