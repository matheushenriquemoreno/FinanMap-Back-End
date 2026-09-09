# Fase 04 — Compartilhamento, desempenho e robustez

| Status       | Pendente   |
|--------------|------------|
| Created      | 2026-09-09 |
| Last Updated | 2026-09-09 |

**Objetivo e resultado esperado:** todos os fluxos respeitam o contexto compartilhado, permanecem previsíveis sob erro e atendem ao volume aprovado de centenas de itens.

**Capacidade ou fluxo coberto:** leitura compartilhada, escrita autorizada, isolamento, carga representativa e regressão integral.

**Requisitos relacionados:** `LCP-BE-05`, `LCP-BE-06`, `LCP-BE-15`, `LCP-BE-16`, `LCP-BE-17`, `EXPECT-BE-02`–`EXPECT-BE-05`.

**Dependências externas:** Fases 01 a 03 concluídas e aprovadas em `review`.

## Tarefa T17 — Aplicar autorização e isolamento a todas as operações

Centralizar a resolução do contexto em `IUsuarioLogado`, permitir leitura ao convidado com visualização, permitir escrita apenas ao proprietário ou convidado com edição e impedir acesso lateral por identificador.

- **Requisitos relacionados:** `LCP-BE-15`, `LCP-BE-16`, `EXPECT-BE-04`, `EXPECT-BE-05`.
- **Referência ao design:** premissa de seguir `CustoFixoService.PodeEditar` e `IdContextoDados`, sem criar permissão específica para a feature.
- **Dependências:** `T05`–`T16`.
- **Parte do sistema afetada:** serviço da feature, consultas por ID e testes de autorização.
- **Testes e verificações:** matriz proprietário/edição/visualização para cada leitura e mutação; IDs de outro proprietário; contexto inexistente ou revogado.
- **Critérios de conclusão:** todas as leituras autorizadas retornam somente o contexto escolhido e toda escrita não autorizada falha sem alterar ou revelar dados.
- **Riscos ou premissas:** o middleware atual preenche corretamente `PermissaoAtual` e `IdContextoDados`.

## Tarefa T18 — Validar carga, índices, precisão e regressão completa

Exercitar consultas e transições com centenas de itens, revisar planos de consulta e índices, confirmar precisão dos totais e consolidar a suíte de regressão da feature sem introduzir limites não previstos.

- **Requisitos relacionados:** `LCP-BE-05`, `LCP-BE-06`, `LCP-BE-17`, `EXPECT-BE-01`–`EXPECT-BE-05`.
- **Referência ao design:** premissas do plano sobre MongoDB, volume de centenas e comandos existentes de qualidade.
- **Dependências:** `T17`.
- **Parte do sistema afetada:** repositório, mappings/índices, testes e OpenAPI da feature.
- **Testes e verificações:** massa com centenas de pendentes/comprados; totais decimais; ordenação; tempo e memória observados; suíte completa, build e formato.
- **Critérios de conclusão:** consultas permanecem utilizáveis, índices apoiam os filtros, totais são exatos e nenhuma regressão é detectada.
- **Riscos ou premissas:** se o volume aprovado exigir paginação para cumprir a expectativa, a mudança de contrato deve ser coordenada com o front-end antes da aprovação.

## Orientações de implementação

- Não confiar na ocultação de ações do front-end para autorização.
- Respostas de itens inexistentes e não autorizados não devem facilitar enumeração de dados.
- Não adicionar limite rígido silencioso; eventual paginação precisa manter acesso a todos os itens.

## Testes e verificações da fase

- `dotnet test Modulos/GerenciamentoMensal/Tests/Tests.csproj`
- `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`
- `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes`
- Smoke autenticado nos três perfis de permissão e com troca de contexto.
- Verificação do OpenAPI final contra as operações consumidas pelo front-end.

## Critérios de aceitação da fase

1. Proprietário e convidado com edição executam todas as mutações autorizadas.
2. Convidado com visualização consulta, mas não altera dados.
3. Nenhuma operação alcança item de outro contexto não autorizado.
4. Centenas de itens permanecem consultáveis com ordem e totais corretos.
5. Suíte, build, formato e smoke final passam.

## Riscos, premissas e dependências externas da fase

- Esta fase libera o contrato final necessário à Fase 04 do front-end.
- Evidência local de volume não substitui monitoramento após publicação, que está fora do escopo deste plano.
