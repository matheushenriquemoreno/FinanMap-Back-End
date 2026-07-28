# Bug: mcp-client-configuration-rejected

**Data:** 2026-07-28
**Status:** resolvido
**Resolvido:** 2026-07-28 - 14d6e3c
**Confiança:** Alta - Codex e Claude exigem formatos diferentes de configuração de cliente, enquanto a UI do FinanMap expunha apenas uma instrução genérica com endpoint.

## Causa Raiz

A implementação MCP expõe um endpoint remoto válido via Streamable HTTP, mas o contrato de configuração apresentado ao usuário está incompleto para clientes reais.

Evidências:

- `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.vue:74` mostra uma seção genérica "Como conectar".
- `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.vue:76-78` orienta o usuário a abrir as configurações de um agente compatível com MCP, informar o endpoint e concluir a autorização no navegador.
- `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.vue:83` expõe apenas `configuration.endpoint`.
- `FinanMap-Back-End/docs/mcp-compatibility.md:6` define o servidor como Streamable HTTP remoto em `/mcp`, sem SSE legado.
- `FinanMap-Back-End/Modulos/GerenciamentoMensal/WebApi/Mcp/McpTransportEndpoints.cs:21-36` mapeia a descoberta de recurso protegido e o endpoint `/mcp` com autorização bearer.

Isso não é suficiente para os dois clientes que o usuário tentou configurar:

- O Codex usa TOML em `[mcp_servers.<nome>]` para configuração local. Um servidor remoto é configurado com `url = "..."`, e o OAuth é iniciado pelo cliente.
- O Claude Code usa flags de CLI ou JSON com campo `type`. Para HTTP remoto, o JSON precisa incluir `{"type":"http","url":"..."}` ou `{"type":"streamable-http","url":"..."}`. Uma entrada JSON com `url` mas sem `type` é tratada pelo Claude Code como configuração inválida ou como um servidor stdio incompleto.

A UI e a documentação atuais deixam o usuário tentando adivinhar uma configuração "universal" que, na prática, Codex e Claude não aceitam literalmente.

## Reprodução

Loop de feedback usado:

1. Inspecionar a UI de configuração MCP apresentada ao usuário:

   ```powershell
   Select-String -Path src\components\Configuracoes\IntegracaoIaConfig.vue -Pattern 'Como conectar|Informe o endpoint|mcp-endpoint'
   ```

   Sinal de falha: apenas o endpoint é exibido; não há TOML para Codex, comando para Claude, JSON para Claude, passo de login/autenticação OAuth nem tipo de transporte por cliente.

2. Inspecionar o contrato de transporte no backend:

   ```powershell
   Select-String -Path docs\mcp-compatibility.md -Pattern 'Transporte|Compatibilidade mínima|cliente precisa|sem SSE'
   Select-String -Path Modulos\GerenciamentoMensal\WebApi\Mcp\McpTransportEndpoints.cs -Pattern 'oauth-protected-resource|MapMcp|RequireAuthorization'
   ```

   Sinal: o servidor é Streamable HTTP/OAuth remoto em `/mcp`, não stdio e não SSE.

3. Comparar com a documentação atual dos clientes:

   - A configuração MCP do Codex aceita servidores Streamable HTTP via `url` no `config.toml` e suporta OAuth.
   - O Claude Code exige `--transport http` ou JSON com `"type":"http"`/`"streamable-http"` para HTTP remoto.

4. Tentativa de confirmação local pelas CLIs:

   ```powershell
   codex mcp --help
   claude mcp --help
   ```

   Resultado nesta sessão: `codex.exe` retornou `Acesso negado` a partir do caminho WindowsApps, e `claude` não estava disponível no PATH. Isso bloqueia a execução direta dos clientes neste ambiente, mas não altera a lacuna confirmada no contrato do produto.

## Hipóteses Testadas

| # | Hipótese | Predição | Resultado |
|---|----------|----------|-----------|
| 1 | O servidor foi implementado com transporte errado para Codex/Claude. | Se fosse verdade, o backend exporia apenas stdio/SSE ou uma rota HTTP que não é MCP. | Refutada. O backend expõe Streamable HTTP em `/mcp` e descoberta OAuth. |
| 2 | A configuração apresentada ao usuário é genérica demais e omite o formato por cliente. | Se fosse verdade, a UI mostraria apenas um endpoint e nenhum snippet aceito por Codex/Claude. | Confirmada. A UI expõe apenas `configuration.endpoint` e instruções genéricas. |
| 3 | O Claude rejeita a configuração quando `type` é omitido no JSON. | Se fosse verdade, a documentação oficial do Claude exigiria `type` para entradas JSON de HTTP remoto. | Confirmada pela documentação. |
| 4 | O Codex rejeita a configuração quando é usado JSON no estilo Claude em vez de TOML. | Se fosse verdade, a documentação oficial do Codex mostraria `[mcp_servers.<nome>]` com `url`, não `mcpServers`. | Confirmada pela documentação. |
| 5 | Restrições de callback OAuth são o bloqueio principal. | Se fosse verdade, o backend rejeitaria URIs de callback localhost usadas pelos clientes. | Não confirmada. Testes existentes exercitam fluxo com callback localhost; manter como cobertura de regressão, mas isso não é a causa raiz aqui. |

## Proposta de Correção

1. Atualizar `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.vue` para substituir a instrução genérica por blocos específicos de configuração por cliente:

   - Codex:

     ```toml
     [mcp_servers.finanmap]
     url = "<configuration.endpoint>"
     auth = "oauth"
     ```

     Incluir o passo operacional: autenticar o servidor no Codex depois de salvar/reiniciar, por exemplo pela ação "Authenticate" na UI ou via `codex mcp login finanmap`.

   - Claude Code CLI:

     ```powershell
     claude mcp add --transport http finanmap <configuration.endpoint>
     ```

     Incluir o passo operacional: usar `/mcp` para autenticar se o Claude marcar o servidor como "Needs authentication".

   - Claude JSON:

     ```json
     {
       "type": "http",
       "url": "<configuration.endpoint>"
     }
     ```

     Informar explicitamente que `type` é obrigatório para configuração JSON no Claude.

2. Adicionar botões de copiar para cada snippet gerado, não apenas para o endpoint. Manter a ação atual de copiar endpoint, porque ela ainda é útil para clientes genéricos.

3. Adicionar testes de front-end em `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.spec.ts` garantindo que:

   - o bloco TOML do Codex contém `[mcp_servers.finanmap]`, `url = "<endpoint>"` e `auth = "oauth"`;
   - o bloco CLI do Claude contém `claude mcp add --transport http finanmap <endpoint>`;
   - o bloco JSON do Claude contém `"type":"http"` e `"url":"<endpoint>"`;
   - nenhuma instrução de bearer token manual ou chave de API seja introduzida.

4. Atualizar `FinanMap-Back-End/docs/mcp-compatibility.md` com uma seção "Configuração de clientes" contendo os mesmos snippets canônicos e o aviso de que não existe um único arquivo de configuração válido para todos os clientes.

5. Adicionar ou atualizar uma checagem leve de prontidão backend/frontend para que a Fase 7 não passe enquanto a UI expõe apenas um endpoint cru. A checagem pode validar a presença das strings de configuração para Codex e Claude no componente ou no teste do front-end.

## Correção Aplicada

A tela de integração MCP agora apresenta blocos específicos para Codex, Claude Code CLI e Claude JSON, cada um com botão de copiar e instrução operacional de autenticação OAuth. A documentação de compatibilidade também passou a registrar que não existe um único formato universal aceito por todos os clientes MCP.

O script de prontidão da Fase 7 valida a presença do TOML do Codex, do comando HTTP do Claude Code e do campo `type` obrigatório no JSON do Claude.

## Teste de Regressão

Ponto principal de regressão: `FinanMap-Front-End/src/components/Configuracoes/IntegracaoIaConfig.spec.ts`.

O teste deve montar o painel de integração com `configuration.endpoint = "https://api.finanmap.test/mcp"` e verificar que os snippets renderizados/copiados são válidos para TOML do Codex e configuração HTTP do Claude.

Ponto secundário de regressão: `FinanMap-Back-End/docs/mcp-compatibility.md` em uma checagem de prontidão, ou um script de readiness de fase existente que valide a presença de orientação específica por cliente.

## Prevenção

Não tratar "agente compatível com MCP" como um único alvo de configuração. Para cada cliente citado no texto do produto, manter um exemplo específico e testá-lo contra o formato aceito documentado por esse cliente.
