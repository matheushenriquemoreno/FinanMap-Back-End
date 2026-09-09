# Fase 03 — Ciclo da compra e integração com despesas

| Status       | Concluída   |
|--------------|------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Objetivo e resultado esperado:** um pendente pode ser concluído, consultado como comprado, opcionalmente vinculado a uma despesa, revertido ou excluído com as regras de preservação aprovadas.

**Capacidade ou fluxo coberto:** ciclo completo entre planejamento, compra realizada e fluxo financeiro mensal.

**Requisitos relacionados:** `LCP-BE-07`–`LCP-BE-14`, `LCP-BE-17`, `EXPECT-BE-01`, `EXPECT-BE-02`, `EXPECT-BE-04`, `EXPECT-BE-05`.

**Dependências externas:** Fases 01 e 02 aprovadas; serviço e repositório de despesas disponíveis.

## Tarefa T10 — Concluir um pendente com valor real e data

Criar a transição de pendente para comprado, validando valor real positivo e data não futura, preservando a estimativa original e impedindo conclusão repetida.

- **Requisitos relacionados:** `LCP-BE-07`, `LCP-BE-08`, `EXPECT-BE-01`, `EXPECT-BE-02`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de encapsular a transição na entidade e orquestrar pelo serviço da feature.
- **Dependências:** `T01`–`T09`.
- **Parte do sistema afetada:** entidade, DTO de conclusão, serviço, repositório e endpoint de ação.
- **Testes e verificações:** data atual/passada; data futura; valor zero; conclusão repetida; item inexistente; consulta antes e depois.
- **Critérios de conclusão:** item aparece em exatamente um estado, mantém a estimativa e registra valor real e data somente em sucesso.
- **Riscos ou premissas:** horários são reduzidos à data de compra observável; não há edição direta após conclusão.

## Tarefa T11 — Consultar comprados com os dados de comparação

Disponibilizar consulta exclusiva dos comprados contendo estimativa original, valor real, data e indicação de vínculo com despesa, sem retornar esses itens entre os pendentes.

- **Requisitos relacionados:** `LCP-BE-08`, `LCP-BE-11`, `EXPECT-BE-02`, `EXPECT-BE-03`.
- **Referência ao design:** premissa de DTO específico de leitura e filtro por proprietário/estado no repositório.
- **Dependências:** `T10`.
- **Parte do sistema afetada:** repositório, DTO de consulta, serviço e endpoint.
- **Testes e verificações:** misturar pendentes e comprados no mesmo contexto e confirmar separação e campos retornados.
- **Critérios de conclusão:** cada coleção contém somente seu estado e comprado preserva os três valores aprovados.
- **Riscos ou premissas:** a lista de comprados não tem limpeza automática.

## Tarefa T12 — Criar e vincular no máximo uma despesa à compra

Estender a conclusão para aceitar opcionalmente mês, ano e categoria, criar uma despesa comum com o valor real e registrar seu identificador no item. A operação deve impedir um segundo vínculo e não confirmar sucesso parcial.

- **Requisitos relacionados:** `LCP-BE-09`, `LCP-BE-10`, `EXPECT-BE-02`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de reutilizar `DespesaService`, `IDespesaRepository` e validação de categoria do contexto; a menor estratégia de consistência suportada deve ser provada por teste.
- **Dependências:** `T10`, disponibilidade do fluxo existente de despesas.
- **Parte do sistema afetada:** DTO e serviço de conclusão, integração com `Application/Despesa` e persistência do vínculo.
- **Testes e verificações:** conclusão sem despesa; com despesa; categoria inválida; falha ao criar despesa; tentativa de segundo vínculo; consulta do Mês a Mês.
- **Critérios de conclusão:** despesa usa o valor real, pertence ao mesmo contexto, vínculo é único e falhas não deixam estado parcialmente confirmado.
- **Riscos ou premissas:** se a consistência entre documentos não puder ser garantida com os mecanismos existentes, interromper e criar design técnico antes de prosseguir.

## Tarefa T13 — Reverter compra sem despesa vinculada

Implementar a reversão simples, descartando valor real e data, mantendo a estimativa original e devolvendo o item à ordenação de pendentes.

- **Requisitos relacionados:** `LCP-BE-12`, `EXPECT-BE-02`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de transição encapsulada na entidade.
- **Dependências:** `T10`, `T11`.
- **Parte do sistema afetada:** entidade, serviço, DTO de reversão e endpoint de ação.
- **Testes e verificações:** reversão válida; repetida; item pendente; inexistente; consulta e total após reversão.
- **Critérios de conclusão:** item retorna apenas aos pendentes com estimativa intacta e sem valor/data reais.
- **Riscos ou premissas:** a data de criação original permanece para o desempate de ordenação.

## Tarefa T14 — Reverter compra com decisão sobre a despesa vinculada

Ao reverter item vinculado, aceitar a escolha explícita de excluir ou preservar a despesa. Se preservada, remover apenas o vínculo; se excluída, remover a despesa e o vínculo sem confirmar reversão parcial.

- **Requisitos relacionados:** `LCP-BE-12`, `LCP-BE-13`, `EXPECT-BE-02`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** regra aprovada no PRD e premissa de reutilizar a exclusão do domínio de despesas.
- **Dependências:** `T12`, `T13`.
- **Parte do sistema afetada:** DTO e serviço de reversão, integração com despesas e persistência do vínculo.
- **Testes e verificações:** preservar despesa; excluir despesa; falha na exclusão; vínculo inexistente; isolamento de contexto.
- **Critérios de conclusão:** escolha é respeitada, vínculo é removido e nenhuma falha deixa item/despesa em estado contraditório.
- **Riscos ou premissas:** a despesa preservada continua sendo uma despesa comum sem marca especial de origem.

## Tarefa T15 — Excluir comprado preservando a despesa existente

Permitir excluir um item comprado e seu vínculo, garantindo que uma despesa vinculada continue disponível no Mês a Mês conforme decisão aprovada.

- **Requisitos relacionados:** `LCP-BE-14`, `EXPECT-BE-02`, `EXPECT-BE-05`.
- **Referência ao design:** regra material confirmada no PRD em 2026-09-09.
- **Dependências:** `T11`, `T12`.
- **Parte do sistema afetada:** serviço, repositório e endpoint de exclusão.
- **Testes e verificações:** excluir comprado sem despesa; excluir com despesa; consultar a despesa após exclusão; verificar outros itens.
- **Critérios de conclusão:** item e vínculo deixam de existir, despesa permanece e nenhum outro registro é alterado.
- **Riscos ou premissas:** a exclusão do item não é reversível.

## Tarefa T16 — Calcular o comparativo agregado dos comprados

Retornar na consulta de comprados a soma das estimativas originais e a soma dos valores reais do contexto atual, atualizadas após conclusão, reversão ou exclusão.

- **Requisitos relacionados:** `LCP-BE-17`, `EXPECT-BE-01`, `EXPECT-BE-03`.
- **Referência ao design:** premissa de agregar no repositório ou serviço sem carregar dados de outro proprietário.
- **Dependências:** `T11`, `T13`–`T15`.
- **Parte do sistema afetada:** consulta de repositório, serviço e DTO de resposta.
- **Testes e verificações:** coleção vazia; múltiplos valores decimais; mudança dos totais em cada transição.
- **Critérios de conclusão:** totais estimado e real correspondem exatamente aos comprados atuais.
- **Riscos ou premissas:** despesas preservadas após exclusão não entram mais no comparativo da lista, pois o item deixou de existir.

## Orientações de implementação

- A conclusão com despesa é opcional; a compra continua válida sem vínculo.
- Reversão e exclusão têm semânticas diferentes para a despesa e devem usar comandos distintos.
- Não expor uma operação de edição direta de comprado.

## Testes e verificações da fase

- `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj`
- `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`
- `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes`
- Smoke completo via API e inspeção do Mês a Mês para criação, preservação e exclusão opcional de despesa.

## Critérios de aceitação da fase

1. Conclusão separa corretamente pendentes e comprados.
2. Vínculo opcional cria uma única despesa com o valor real.
3. Reversão preserva ou exclui a despesa conforme escolha explícita.
4. Exclusão de comprado sempre preserva a despesa vinculada.
5. Comparativos refletem o conjunto atual de comprados.
6. Falhas exercitadas não deixam estados parciais.

## Riscos, premissas e dependências externas da fase

- O maior risco é a consistência entre registros de compra e despesa; o `review` da fase deve exigir evidência específica.
- Esta fase libera o contrato necessário à Fase 03 do front-end.

## Registro de execução

- Implementados `POST /{id}/comprar`, `GET /compradas`, `POST /{id}/reverter` e a exclusão de comprado com preservação da despesa.
- A integração usa `ICompraPlanejadaDespesaGateway` sobre `IDespesaService`; a leitura do acumulado mensal foi movida para antes da inserção da despesa para evitar falha posterior conhecida sem identificador compensável.
- `CompraPlanejadaLifecycleTests` cobre conclusão, separação, vínculo único, falhas, compensação, reversão nas duas escolhas, exclusão e agregados.
- Verificação: `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 66 aprovados; build da solução aprovado; format-check limitado à feature aprovado.
- Limitação: sem smoke autenticado/persistência Mongo neste ambiente.
