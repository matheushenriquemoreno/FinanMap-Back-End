# Smoke autenticado com MCP Inspector

O smoke usa somente infraestrutura efêmera de teste:

- MongoDB standalone em Testcontainers, preso a um digest conhecido;
- usuário autenticado e esquema de autenticação disponíveis apenas no assembly `Tests`;
- cliente OAuth público criado por DCR real;
- Authorization Code com PKCE `S256`;
- access token OpenIddict de dez minutos, mantido apenas em memória;
- MCP Inspector CLI fixado em `0.21.2`.

Nenhum endpoint de fixture ou bypass de autenticação é compilado no `WebApi`.
O container Mongo e o host Kestrel são encerrados ao final, invalidando todos os
dados e tokens do smoke.

Execute:

```powershell
dotnet test Tests/Tests.csproj --configuration Release --filter FullyQualifiedName~McpAuthenticatedInspectorSmokeTests
```

Se o Windows Smart App Control bloquear assemblies locais não assinados, use o
runner isolado:

```powershell
docker build -t finanmap-mcp-inspector-tests -f Tests/InspectorSmoke.Dockerfile .
docker run --rm `
  -v /var/run/docker.sock:/var/run/docker.sock `
  -v "${PWD}:/workspace" `
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal `
  finanmap-mcp-inspector-tests `
  dotnet test Tests/Tests.csproj --configuration Release `
  --filter FullyQualifiedName~McpAuthenticatedInspectorSmokeTests
```

O teste faz DCR, prova aprovação e negação do consentimento, emite um token e
chama `finanmap_categories_list` de forma autenticada pelo Inspector. O token
nunca é impresso; se o Inspector o repetir em uma mensagem de erro, o teste
substitui o valor por `[REDACTED]`.
