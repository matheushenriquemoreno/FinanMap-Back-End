# Compatibilidade do MCP Financeiro

## Baseline da V1

- Protocolo MCP: `2025-11-25`.
- Transporte: Streamable HTTP remoto em `/mcp`, sem SSE legado.
- SDK oficial C#: `ModelContextProtocol.AspNetCore` `1.4.1`.
- Autorização: OAuth 2.1 Authorization Code com PKCE obrigatório `S256`.
- Provedor: OpenIddict `7.6.0`, com persistência MongoDB.
- Runtime mínimo do servidor: .NET 9.
- Audience obrigatória dos tokens: `${MCP_PUBLIC_BASE_URL}/mcp`.
- Fora de `Development`, as chaves X.509 persistentes são obrigatórias via
  `MCP_SIGNING_CERTIFICATE_PATH` e `MCP_ENCRYPTION_CERTIFICATE_PATH`; as senhas
  opcionais usam as variáveis homônimas terminadas em `_PASSWORD`.

A integração usa apenas descoberta, autorização e contratos públicos de MCP/OAuth. Nenhuma
ferramenta depende de API privada de um fornecedor de agentes.

## Chave de assinatura dos cursores

`MCP_CURSOR_SIGNING_KEY` é obrigatória quando o endpoint MCP está ativo fora de
`Development`. Gere ao menos 32 bytes criptograficamente aleatórios, por exemplo em
PowerShell:

```powershell
[Convert]::ToBase64String(
    [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
)
```

Armazene o valor exclusivamente no gerenciador de segredos do ambiente; não o grave no
repositório, em arquivos de configuração versionados ou em logs. Todas as instâncias MCP
do mesmo ambiente devem receber exatamente a mesma chave, que precisa permanecer estável
entre reinícios e substituições de instância. Caso contrário, cursores ainda válidos serão
rejeitados.

A rotação deve ser coordenada: interrompa a emissão de cursores pela versão anterior,
implante a mesma chave nova em todas as instâncias e oriente os clientes a reiniciar
paginação em andamento. A troca invalida intencionalmente todos os cursores emitidos com
a chave anterior; cursores não são dados persistentes nem devem ser reaproveitados após a
rotação.

## Escritas em duas etapas

As ferramentas de escrita são publicadas somente quando
`MCP_WRITE_TOOLS_ENABLED=true`. Categorias e receitas usam uma prévia imutável antes de
qualquer efeito financeiro. A prévia:

- é vinculada ao titular, à conexão, ao `requestId` e ao hash canônico do payload;
- expira para confirmação depois de 15 minutos;
- exige `APPLY_CHANGES` para criação/alteração ou `DELETE_PERMANENTLY` para exclusão;
- pode ser confirmada ou cancelada apenas uma vez;
- mantém o payload estruturado criptografado por no máximo 24 horas.

`MCP_PREVIEW_ENCRYPTION_KEY` é obrigatória fora de `Development` quando as ferramentas de
escrita estão ativas. O valor deve ser base64 de exatamente 32 bytes aleatórios, ser
mantido no gerenciador de segredos e permanecer igual em todas as instâncias do ambiente.
A rotação deve preservar a chave anterior enquanto existirem prévias ou operações reconciliáveis;
trocar a chave imediatamente torna esses payloads irrecuperáveis e força classificação
segura como `Unknown`.

O MongoDB standalone não oferece transação entre prévia, journal e registro financeiro.
Por isso, `McpOperationJournal` é criado antes do efeito e funciona como auditoria
canônica, recibo idempotente e fonte para consulta de status. Leases, compare-and-set e
marcadores `McpOperationId`/`LastMcpOperationId` permitem comprovar ou retomar um efeito
com o mesmo `operationId`.

O reconciliador consulta leases vencidos. Ele só retoma dentro da janela operacional de
15 minutos e depois de procurar o marcador do efeito. Uma exclusão cujo alvo já está
ausente permanece `Unknown` quando a causalidade não pode ser provada; ela nunca é
repetida automaticamente. Clientes devem consultar `finanmap_operation_status_get` e não
reenviar uma escrita com resultado `unknown`.

## Compatibilidade mínima

O cliente precisa implementar Streamable HTTP da revisão `2025-11-25`, descoberta OAuth,
Authorization Code, PKCE `S256` e bearer token no cabeçalho. Clientes de navegador enviam
`Origin`; clientes nativos podem omiti-lo. O servidor não oferece o transporte SSE legado.

## Configuração de clientes

Não existe um único arquivo de configuração aceito por todos os clientes MCP. O endpoint
oficial deve ser configurado no formato específico de cada cliente.

Use `${MCP_PUBLIC_BASE_URL}/mcp` como endpoint MCP.

### Codex

Adicione o servidor ao `config.toml` do Codex:

```toml
[mcp_servers.finanmap]
url = "${MCP_PUBLIC_BASE_URL}/mcp"
auth = "oauth"
```

Depois de salvar e reiniciar o cliente quando solicitado, autentique a conexão pela ação de
autenticação do Codex ou pelo comando:

```powershell
codex mcp login finanmap
```

### Claude Code

Para adicionar pela CLI:

```powershell
claude mcp add --transport http finanmap ${MCP_PUBLIC_BASE_URL}/mcp
```

Se o Claude marcar o servidor como `Needs authentication`, use `/mcp` no Claude Code para
concluir o OAuth no navegador.

Para configuração via JSON, o campo `type` é obrigatório:

```json
{
  "type": "http",
  "url": "${MCP_PUBLIC_BASE_URL}/mcp"
}
```

Uma entrada JSON com `url` mas sem `type` não é um contrato válido para o Claude Code.

## Política de atualização

Versões preview, release candidate e a linha `2.x` do SDK não entram na V1. Uma atualização
de protocolo ou SDK só pode ocorrer depois de:

1. revisão das mudanças incompatíveis e dos avisos de segurança;
2. aprovação da suíte de contrato e conformidade;
3. validação de descoberta e chamada com pelo menos dois clientes MCP independentes;
4. confirmação de que autorização, revogação, isolamento por conta e auditoria continuam verdes;
5. registro explícito da nova baseline neste documento e em `McpProtocolContract`.

Mudanças incompatíveis em nomes ou schemas de ferramentas exigem uma nova versão de
ferramenta; a evolução normal dos schemas é somente aditiva.
