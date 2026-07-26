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

## Compatibilidade mínima

O cliente precisa implementar Streamable HTTP da revisão `2025-11-25`, descoberta OAuth,
Authorization Code, PKCE `S256` e bearer token no cabeçalho. Clientes de navegador enviam
`Origin`; clientes nativos podem omiti-lo. O servidor não oferece o transporte SSE legado.

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
