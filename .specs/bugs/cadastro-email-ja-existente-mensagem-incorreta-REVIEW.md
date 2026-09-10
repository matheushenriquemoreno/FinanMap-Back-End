# Review — cadastro de e-mail já existente

| Status       | Aprovado   |
| ------------ | ---------- |
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Escopo revisado:** correção do bug `cadastro-email-ja-existente-mensagem-incorreta`

**Versão da avaliação:** 1

## Artefatos analisados

- Relatório do bug: `.specs/bugs/cadastro-email-ja-existente-mensagem-incorreta.md`.
- Código alterado: `Modulos/GerenciamentoMensal/Application/Login/Services/LoginService.cs`.
- Ambiente local: `docker-compose.yml`, Mongo, API e frontend em execução.
- Suíte automatizada: `Modulos/GerenciamentoMensal/FinancasPessoais.sln`.

PRD, design técnico e plano não se aplicam ao fluxo de correção deste defeito.

## Resumo executivo

A revisão independente confirmou a causa no ramo de e-mail já existente de `LoginService.CriarUsuario`. A correção é mínima: mantém HTTP 422 e substitui somente a mensagem enganosa por `E-mail já cadastrado!`. A reprodução original falhou antes da correção e a regressão passou depois; o caso de formato inválido permaneceu distinto. Veredito: **Aprovado**; bug fechado.

## Resultado das verificações obrigatórias

| Verificação                   | Resultado | Evidência                                                                                                                                                                |
| ----------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Reprodução original           | Atendida  | POST para `http://localhost:7170/api/login/Create` com o e-mail existente retornou HTTP 422; Mongo confirmou um registro e o log confirmou a tentativa duplicada.        |
| Causa confirmada              | Atendida  | `LoginService.CriarUsuario`, linha 160, é o ramo executado quando `GetByEmail` encontra o usuário.                                                                       |
| Regressão de duplicidade      | Atendida  | Após rebuild da API: HTTP 422 com `{"errors":["E-mail já cadastrado!"]}`.                                                                                                |
| Distinção de formato inválido | Atendida  | `email-invalido` continuou retornando HTTP 400 com `E-mail informado invalido!`.                                                                                         |
| Testes automatizados          | Atendida  | `dotnet test Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore`: 66 aprovados, 0 falhas, 0 ignorados.                                                        |
| Build da imagem               | Atendida  | `docker compose --env-file .env.local build api`: concluído com 0 erros.                                                                                                 |
| Saúde do ambiente             | Atendida  | Mongo, API e frontend em estado `healthy`; `/healthcheck` da API retornou `Healthy`; frontend retornou HTTP 200.                                                         |
| Escopo                        | Atendida  | A alteração funcional revisada é uma única linha em `LoginService.cs`; o relatório do bug é o único artefato novo deste fluxo.                                           |
| Complexidade ciclomática      | Atendida  | Não houve nova função nem novo ramo; alteração textual dentro de um `if` existente, portanto não aumenta a complexidade.                                                 |
| Complexidade algorítmica      | Atendida  | Não houve mudança em consultas, loops, coleções ou I/O; o caminho existente continua fazendo uma busca indexável por e-mail.                                             |
| Segurança, dados e operação   | Atendida  | Nenhum cadastro existente foi removido ou alterado; a duplicidade continua sendo rejeitada e o erro de Resend permanece identificado como problema operacional separado. |

## Matriz de rastreabilidade

| Requisito                      | Código                                                  | Teste                                                        | Evidência                                                                       | Status     |
| ------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------- | ---------- |
| Rejeitar e-mail já cadastrado  | `LoginService.CriarUsuario`, ramo `usuario is not null` | POST de regressão com `matheushenriquesoares35@gmail.com`    | HTTP 422 e mensagem `E-mail já cadastrado!` após rebuild                        | Comprovado |
| Preservar validação de formato | Construtor de `Usuario` e tratamento existente          | POST de regressão com `email-invalido`                       | HTTP 400 e mensagem `E-mail informado invalido!`                                | Comprovado |
| Não alterar dados existentes   | Retorno antecipado antes de `Add`                       | Consulta Mongo antes da reprodução e resposta de duplicidade | O registro existente foi preservado; nenhuma operação de inclusão foi executada | Comprovado |

## Achados

Nenhum achado bloqueador, alto, médio ou baixo.

## Riscos residuais e ressalvas aceitas

- O fluxo de envio do código de login ainda possui um problema operacional independente: os logs indicam que o domínio `devmoreno.online` não está verificado no Resend. Isso não afeta a resposta de duplicidade, mas pode impedir o recebimento de códigos para login até a configuração do provedor ser corrigida.

## Veredito

**Veredito:** Aprovado

**Fundamentação:** a causa foi confirmada, a mensagem incorreta deixou de ocorrer, a regressão passou, a distinção de formato foi preservada, a suíte .NET passou integralmente e o ambiente permaneceu saudável. A correção é pequena e não altera dados nem amplia o escopo.

## Próxima ação

Bug fechado. Para este e-mail, usar o fluxo de login em vez do cadastro; se o código não chegar, tratar separadamente a verificação do domínio no Resend.

## Histórico de revisões anteriores

| Versão | Data       | Veredito | Resumo                                                                          |
| ------ | ---------- | -------- | ------------------------------------------------------------------------------- |
| 1      | 2026-09-09 | Aprovado | Correção confirmada por regressão HTTP, suíte .NET e ambiente Compose saudável. |
