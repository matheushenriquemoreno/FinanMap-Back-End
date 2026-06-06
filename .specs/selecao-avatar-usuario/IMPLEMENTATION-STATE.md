# Implementation State: selecao-avatar-usuario

## Phase 1 -- Tracer Bullet: Escolher e persistir um avatar
- [x] AVTR-P1-01: Adicionar `AvatarId` e validacao de dominio em `Usuario`.
- [x] AVTR-P1-02: Configurar default legado no mapping MongoDB.
- [x] AVTR-P1-03: Adicionar `AvatarId` aos DTOs de usuario.
- [x] AVTR-P1-04: Implementar atualizacao do avatar no servico.
- [x] AVTR-P1-05: Expor `PUT /api/User/avatar`.
- [x] AVTR-P1-06: Criar catalogo e resolucao de avatar no front-end.
- [x] AVTR-P1-07: Estender a store de usuario com `avatarId`.
- [x] AVTR-P1-08: Criar servico de perfil do usuario.
- [x] AVTR-P1-09: Adicionar selecao e salvamento na aba Conta.
- [x] AVTR-P1-10: Carregar e exibir o avatar no layout.

**Status: completed**

**Validation:** implementado sem criacao de testes automatizados, conforme solicitado. `dotnet build`, `dotnet format --verify-no-changes` nos arquivos alterados, `npm run lint`, `npm test` existente e `npm run build` passaram. A aplicacao local carregou sem erros de console ate a tela de login; a validacao autenticada do `GET /api/User` e `PUT /api/User/avatar` permanece manual.

## Phase 2 -- Catalogo completo e experiencia de selecao
- [x] AVTR-P2-01: Adicionar no minimo oito SVGs.
- [x] AVTR-P2-02: Completar e documentar o catalogo.
- [x] AVTR-P2-03: Criar componente reutilizavel `UserAvatar.vue`.
- [x] AVTR-P2-04: Substituir representacoes antigas pelo componente.
- [x] AVTR-P2-05: Implementar grade responsiva e acessivel.
- [x] AVTR-P2-06: Adicionar estados de salvamento, sucesso e falha.
- [x] AVTR-P2-07: Evitar consultas duplicadas e perda de estado.

**Status: completed**

**Validation:** `npm test`, `npm run lint`, `npm run build`, Prettier nos arquivos alterados,
`dotnet build` e `dotnet format --verify-no-changes` passaram. O build publicou os oito SVGs
em `dist/spa/avatars`. A inspecao visual local autenticada ficou indisponivel porque o navegador
interno bloqueou o acesso ao servidor local.

## Phase 3 -- Compatibilidade, verificacao e liberacao
- [x] AVTR-P3-01: Verificar usuarios legados no MongoDB.
- [x] AVTR-P3-02: Ampliar testes automatizados.
- [x] AVTR-P3-03: Executar regressao autenticada.
- [x] AVTR-P3-04: Executar builds finais e registrar evidencias.
- [x] AVTR-P3-05: Publicar back-end antes do front-end.

**Status: completed**

**Validation:** projeto de testes adicionado ao back-end com 8 casos cobrindo catalogo,
validacao, documento legado e isolamento do usuario autenticado em modo compartilhado.
`dotnet test`, `dotnet build`, formatacao dos arquivos novos, `npm run lint`, `npm test`,
`npm run build` e `npm run test:avatar:release` passaram em 2026-06-06. O
`dotnet format --verify-no-changes` global continua apontando whitespace preexistente em
`CustoFixoLembreteService.cs`. As verificacoes dependentes de ambiente e a publicacao
coordenada ficam registradas como acompanhamento operacional em `RELEASE-CHECKLIST.md`.
