# Fase 02 — Gestão dos itens pendentes

| Status       | Concluída  |
| ------------ | ---------- |
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Objetivo e resultado esperado:** itens pendentes podem ser alterados e excluídos com validação, isolamento e respostas de erro previsíveis.

**Capacidade ou fluxo coberto:** completar o CRUD do estado pendente sem introduzir transições de compra.

**Requisitos relacionados:** `LCP-BE-02`, `LCP-BE-03`, `LCP-BE-04`, `EXPECT-BE-01`, `EXPECT-BE-04`, `EXPECT-BE-05`.

**Dependências externas:** Fase 01 concluída e aprovada em `review`.

## Tarefa T06 — Alterar todos os campos permitidos de um pendente

Adicionar a operação de atualização que localiza o item no contexto do proprietário, aplica todas as validações de cadastro e preserva identidade, estado e data de criação.

- **Requisitos relacionados:** `LCP-BE-02`, `LCP-BE-04`, `EXPECT-BE-01`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de atualização via entidade e serviço, seguindo `CustoFixoService.Atualizar` como padrão local.
- **Dependências:** `T01`–`T05`.
- **Parte do sistema afetada:** entidade, DTO de atualização, serviço, repositório e endpoint da feature.
- **Testes e verificações:** atualizar cada campo, substituir lista de links, rejeitar valores inválidos, item inexistente e item de outro contexto.
- **Critérios de conclusão:** todos os campos editáveis persistem; campos imutáveis não mudam; falhas não alteram o item.
- **Riscos ou premissas:** somente itens pendentes admitem edição nesta versão.

## Tarefa T07 — Excluir somente o pendente solicitado

Adicionar a exclusão de item pendente, garantindo que a busca e a remoção estejam limitadas ao contexto do proprietário e que outros itens permaneçam intactos.

- **Requisitos relacionados:** `LCP-BE-03`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de seguir o fluxo de exclusão dos serviços existentes e retornar `NotFound` para alvo não acessível.
- **Dependências:** `T03`–`T05`.
- **Parte do sistema afetada:** serviço, repositório e endpoint da feature.
- **Testes e verificações:** excluir item existente; repetir exclusão; tentar excluir item alheio; conferir total após remoção.
- **Critérios de conclusão:** somente o alvo autorizado é removido e o total da consulta seguinte é recalculado corretamente.
- **Riscos ou premissas:** não existe restauração automática de item excluído.

## Tarefa T08 — Uniformizar validações e falhas das mutações de pendentes

Garantir que falhas de domínio, item ausente e entrada inválida sejam convertidas no padrão `Result`/HTTP já adotado, sem confirmar atualização ou exclusão incompleta.

- **Requisitos relacionados:** `LCP-BE-02`, `LCP-BE-03`, `LCP-BE-04`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de reutilizar `Error.Validation`, `Error.NotFound` e os mapeadores de resultado existentes.
- **Dependências:** `T06`, `T07`.
- **Parte do sistema afetada:** serviço de aplicação e endpoints da feature.
- **Testes e verificações:** matriz de entradas inválidas e códigos HTTP; confirmar estado anterior após cada falha.
- **Critérios de conclusão:** consumidor distingue validação de item não encontrado e nenhuma falha deixa mudança parcial.
- **Riscos ou premissas:** mensagens seguem o padrão atual em pt-BR sem expor detalhes internos.

## Tarefa T09 — Consolidar a regressão automatizada do CRUD de pendentes

Cobrir o CRUD da fase com testes focados de domínio e aplicação, usando fakes ou dublês no mesmo estilo dos testes existentes, e registrar verificações manuais somente onde a infraestrutura atual não automatiza o endpoint.

- **Requisitos relacionados:** `LCP-BE-01`–`LCP-BE-06`, `EXPECT-BE-01`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de usar xUnit no projeto `Tests` sem introduzir framework adicional.
- **Dependências:** `T06`–`T08`.
- **Parte do sistema afetada:** `Tests/` e documentação da fase.
- **Testes e verificações:** executar suíte do projeto, build, formato e smoke manual do CRUD protegido.
- **Critérios de conclusão:** cenários de sucesso, inválidos, ausentes e isolamento estão automatizados ou têm evidência manual explícita.
- **Riscos ou premissas:** testes de persistência real continuam como smoke local se não houver harness MongoDB no projeto.

## Orientações de implementação

- A busca por ID deve validar também o proprietário do contexto.
- Não reutilizar uma operação genérica que permita editar item comprado.
- Atualização e exclusão devem refletir imediatamente os totais consultados.

## Testes e verificações da fase

- `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj`
- `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`
- `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes`
- Smoke autenticado de atualização, exclusão e consulta subsequente.

## Critérios de aceitação da fase

1. Todos os campos de pendente podem ser alterados validamente.
2. Exclusão remove somente o alvo e atualiza o total.
3. Entradas inválidas e alvos não autorizados não alteram dados.
4. A regressão de criação e consulta da Fase 01 permanece verde.

## Riscos, premissas e dependências externas da fase

- Esta fase libera o contrato necessário à Fase 02 do front-end.

## Execução

| Tarefa | Status    | Evidência                                                                                                                                                                                |
| ------ | --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| T06    | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — atualização e validações aprovadas.                                                                          |
| T07    | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — exclusão contextual, repetição e total aprovados.                                                            |
| T08    | Concluída | `dotnet test ... --filter FullyQualifiedName~CompraPlanejadaServiceTests` — falhas de validação de campos e links retornam `Result.Validation` sem mutação.                              |
| T09    | Concluída | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 50 testes aprovados; regressão de criação, consulta, atualização, exclusão, isolamento e falhas consolidada. |

## Encerramento da fase

- Gate completo: `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore` — 50 testes aprovados.
- Gate completo: `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — build aprovado.
- Format-check da feature: `dotnet format ... --verify-no-changes --include` — aprovado.
- Limitação herdada resolvida no ambiente integrado: smoke autenticado contra API/Mongo foi executado; permanecem pendentes apenas inspeção dedicada do plano e métricas formais.
