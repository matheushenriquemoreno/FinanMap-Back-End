# Fase 01 — Tracer bullet de cadastro e consulta

| Status       | Em execução |
|--------------|------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Objetivo e resultado esperado:** um usuário autenticado cria um item planejado válido e o recupera na consulta de pendentes com links, total estimado e ordenação definidos.

**Capacidade ou fluxo coberto:** primeiro caminho completo `Domain` → `Application` → `Infra.data` → `WebApi`.

**Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-04`, `LCP-BE-05`, `LCP-BE-06`, `EXPECT-BE-01`, `EXPECT-BE-03`, `EXPECT-BE-04`, `EXPECT-BE-05`.

**Dependências externas:** MongoDB configurado para o ambiente local; nenhuma dependência do front-end.

## Tarefa T01 — Modelar e validar o item de compra planejada

Criar a entidade de domínio com identidade, proprietário, nome, estimativa, prioridade, descrição opcional, links, estado pendente, data de criação e campos de compra inicialmente ausentes. As invariantes de nome, valores, prioridade, URL e nome da loja devem falhar antes da persistência.

- **Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-04`, `EXPECT-BE-01`, `EXPECT-BE-04`.
- **Referência ao design:** premissa de reutilizar `EntityBase` e `DomainValidator` no projeto `Domain`.
- **Dependências:** nenhuma.
- **Parte do sistema afetada:** `Domain/CompraPlanejada/Entity/`, enums e objetos de valor estritamente necessários.
- **Testes e verificações:** testes unitários para construção válida; nome vazio; valores zero/negativo; prioridade fora do conjunto; URL ou loja inválida; estimativa decimal.
- **Critérios de conclusão:** entidade compila, mantém estimativa válida e rejeita cada entrada inválida sem produzir estado parcial.
- **Riscos ou premissas:** valores monetários seguem o tipo já usado por `Despesa`; divergência exige análise técnica antes de persistir dados.

## Tarefa T02 — Registrar o mapeamento persistente e os índices mínimos

Mapear a nova entidade na infraestrutura MongoDB, incluindo identificadores, links, estado e datas, e registrar índices coerentes com isolamento por proprietário, estado e ordenação da consulta.

- **Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-04`, `LCP-BE-06`, `EXPECT-BE-03`.
- **Referência ao design:** premissa de seguir `IMongoMapping` e os mappings existentes em `Infra.data/Mongo/Mappings`.
- **Dependências:** `T01`.
- **Parte do sistema afetada:** `Infra.data/Mongo/Mappings/` e registro automático de mappings já existente.
- **Testes e verificações:** inicializar a aplicação contra MongoDB local; inspecionar criação da coleção e dos índices; persistir e reler item com dois links.
- **Critérios de conclusão:** aplicação inicia sem erro, o documento é serializado integralmente e os índices planejados existem.
- **Riscos ou premissas:** índices devem apoiar a consulta sem impor unicidade não prevista pelo PRD.

## Tarefa T03 — Disponibilizar o repositório de pendentes com total e ordenação

Criar o contrato e a implementação do repositório para adicionar itens e consultar os pendentes do proprietário atual, ordenados por prioridade e data de criação, retornando os dados necessários ao total estimado.

- **Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-04`, `LCP-BE-05`, `LCP-BE-06`, `EXPECT-BE-03`.
- **Referência ao design:** premissa de estender `RepositoryMongoBase<T>` e filtrar sempre por `UsuarioId`/contexto de dados.
- **Dependências:** `T01`, `T02`.
- **Parte do sistema afetada:** `Domain/CompraPlanejada/Repository/` e `Infra.data/Mongo/Repositorys/`.
- **Testes e verificações:** validar coleção vazia, isolamento entre dois proprietários, ordenação Alta/Média/Baixa e desempate do mais recente para o mais antigo.
- **Critérios de conclusão:** a consulta não mistura contextos, retorna a ordem aprovada e permite calcular a soma exata das estimativas.
- **Riscos ou premissas:** o volume inicial é de centenas de itens, sem paginação obrigatória nesta versão.

## Tarefa T04 — Orquestrar criação e consulta na camada de aplicação

Criar DTOs, interface e serviço de aplicação para cadastrar e listar pendentes. A resposta de consulta deve conter a coleção ordenada e o total estimado atual, sem expor estruturas internas do banco.

- **Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-04`, `LCP-BE-05`, `LCP-BE-06`, `EXPECT-BE-01`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de seguir os serviços existentes, `Result` e `IUsuarioLogado.IdContextoDados`.
- **Dependências:** `T03`.
- **Parte do sistema afetada:** `Application/CompraPlanejada/DTOs/`, `Interfaces/` e `Service/`.
- **Testes e verificações:** testes unitários do serviço com repositórios controlados para sucesso, validação e total de vários itens.
- **Critérios de conclusão:** criação vincula o proprietário correto; consulta vazia retorna total zero; soma e ordem coincidem com os dados válidos.
- **Riscos ou premissas:** o DTO será a fonte do contrato consumido pelo front-end e deve evitar nomenclatura ambígua.

## Tarefa T05 — Publicar e documentar o contrato mínimo da API

Expor criação e consulta no grupo protegido `/api/compras-planejadas`, registrar serviço e repositório na configuração de dependências e incluir os endpoints no OpenAPI existente.

- **Requisitos relacionados:** `LCP-BE-01`, `LCP-BE-05`, `LCP-BE-06`.
- **Referência ao design:** premissa de seguir `MapGroup`, `MapResultCreated`, `MapResult`, `EndpointConfiguration` e `Setup` existentes.
- **Dependências:** `T04`.
- **Parte do sistema afetada:** `WebApi/Controllers/`, `WebApi/Configs/EndpointConfiguration.cs` e `Infra.data/Configure/Setup.cs`.
- **Testes e verificações:** exercitar `POST` e `GET` autenticados pelo Scalar/OpenAPI; executar testes, build e verificação de formato da fase.
- **Critérios de conclusão:** endpoints protegidos respondem com os códigos esperados, aparecem no OpenAPI e o fluxo criado pode ser consultado.
- **Riscos ou premissas:** qualquer alteração posterior do contrato deve permanecer compatível até o front-end correspondente ser ajustado.

## Orientações de implementação

- Manter nomes de domínio em português e responsabilidades alinhadas aos módulos existentes.
- Não criar categoria, prazo, orçamento ou edição de comprado, pois estão fora do PRD.
- Não calcular totais a partir de itens de outro proprietário ou de itens comprados.

## Testes e verificações da fase

- `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj`
- `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`
- `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes`
- Fluxo manual autenticado de criação e consulta no OpenAPI/Scalar com MongoDB local.

## Critérios de aceitação da fase

1. Um item válido com dois links é criado e recuperado integralmente.
2. Dados inválidos são rejeitados sem persistência.
3. A consulta vazia retorna coleção vazia e total zero.
4. Múltiplos itens retornam total e ordenação corretos.
5. Build, testes e verificações de formato aplicáveis passam.

## Riscos, premissas e dependências externas da fase

- MongoDB local precisa estar disponível para a verificação de persistência.
- O contrato criado será dependência externa da Fase 01 do front-end.

## Execução

| Tarefa | Status | Evidência |
|--------|--------|-----------|
| T01 | Concluída | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore --filter FullyQualifiedName~CompraPlanejadaDomainTests` — 9 testes aprovados. |
| T02 | Concluída | `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — build da solução aprovado; mapping e índice composto adicionados. Smoke Mongo pendente de ambiente. |
| T03 | Concluída | `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore` — repositório com filtro obrigatório de contexto, estado e ordenação aprovado. Smoke Mongo pendente de ambiente. |
| T04 | Concluída | `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj --no-restore --filter FullyQualifiedName~CompraPlanejadaServiceTests` — 4 testes aprovados; DTO, contexto, links e total decimal validados. |
| T05 | Pendente | — |
