# Implementation Plan — Selecao de Avatar do Usuario

| Field        | Value                            |
| ------------ | -------------------------------- |
| Tech Lead    | @Usuario                         |
| Team         | Solo developer                   |
| Epic/Ticket  | Selecao de avatar do usuario     |
| Status       | Draft                            |
| Created      | 2026-06-06                       |
| Last Updated | 2026-06-06                       |

## Overview

Permitir que o usuario autenticado escolha seu avatar entre pelo menos oito opcoes fixas e veja a escolha refletida apenas nas superficies do proprio perfil:

- botao e menu de conta do cabecalho;
- apresentacao do usuario no menu lateral;
- cabecalho da aba `Conta` nas configuracoes.

O back-end persistira somente um identificador estavel e validado, como `avatar-01`. Os arquivos visuais ficarao versionados no front-end em `public/avatars/`, evitando upload de arquivos, armazenamento binario, URLs externas e migracoes complexas.

Usuarios existentes que nao possuam o novo campo receberao `avatar-01` como fallback. O avatar nao sera incluido nos contratos ou telas de compartilhamento e somente o usuario autenticado podera alterar a propria escolha.

**Technical Design**: Not provided

**Repository ownership**: Este plano e versionado no repositorio
`FinanMap-Back-End`, que concentra o contrato e a persistencia da preferencia.
As tarefas de interface identificadas no documento devem ser implementadas no
branch correspondente do repositorio `FinanMap-Front-End`.

**Stack avaliada**:

- Front-end: Vue 3, Quasar, Pinia, TypeScript e Axios.
- Back-end: .NET 9, Minimal APIs, camadas Domain/Application/Infra/WebApi e MongoDB.

### Diagnostico atual

- `FinanMap-Front-End/public/avatar.svg` e usado de forma fixa em dois pontos de `MainLayout.vue`.
- O botao de conta no cabecalho usa apenas o icone `person`, sem o avatar atual.
- `InformacoesConta.vue` consulta `GET /api/User`, mas mostra somente a inicial do nome.
- `UserEmail-Store.ts` guarda apenas nome e e-mail; o nome ainda e lido diretamente do `localStorage` em `MainLayout.vue`.
- `GET /api/User` retorna apenas `nome` e `email`.
- A entidade `Usuario` ja e persistida no MongoDB e atualizada pelo repositorio generico com `ReplaceOneAsync`.
- Nao existem suites automatizadas configuradas nos dois projetos; a validacao atual depende de build, lint e testes manuais.

### Abordagem recomendada

- Criar um catalogo fechado de IDs aceitos no back-end e validar qualquer alteracao contra ele.
- Adicionar `AvatarId` na entidade `Usuario`, com default `avatar-01` no dominio e no mapping do MongoDB.
- Incluir `avatarId` no retorno de `GET /api/User`.
- Criar `PUT /api/User/avatar`, protegido por autenticacao, recebendo somente `{ "avatarId": "avatar-03" }`.
- Nao receber `usuarioId` no endpoint; usar sempre `IUsuarioLogado.Id` para impedir alteracao de outro perfil.
- Manter os contratos de login, refresh token e compartilhamento inalterados.
- No front-end, centralizar catalogo, fallback e resolucao de caminho em um helper/modelo compartilhado.
- Estender a store de usuario existente com `avatarId`; carregar o perfil no `MainLayout` e atualizar a store imediatamente apos salvar.
- Extrair um componente visual reutilizavel para evitar regras diferentes entre cabecalho, drawer e configuracoes.

## Scope

### Included

- Oito ou mais avatares fixos versionados no front-end.
- Persistencia da escolha no documento `Usuario`.
- Seletor de avatar na aba `Conta`.
- Atualizacao reativa nas superficies do proprio perfil.
- Fallback para usuarios legados e IDs desconhecidos.
- Validacao server-side da lista permitida.
- Suporte a desktop, mobile, light mode e dark mode.

### Not Included

- Upload de foto personalizada.
- Edicao, recorte ou remocao de imagens.
- Avatar em JWT, login ou refresh token.
- Exibicao do avatar para convidados, proprietarios ou outros usuarios nas telas de compartilhamento.
- Alteracao de nome, e-mail ou outros dados da conta.

## API Contract

### Obter perfil

`GET /api/User`

Resposta:

```json
{
  "nome": "Nome do Usuario",
  "email": "usuario@exemplo.com",
  "avatarId": "avatar-03"
}
```

### Atualizar avatar

`PUT /api/User/avatar`

Requisicao:

```json
{
  "avatarId": "avatar-03"
}
```

Resposta recomendada:

```json
{
  "avatarId": "avatar-03"
}
```

Erros:

- `400 Bad Request`: identificador vazio ou fora do catalogo permitido.
- `401 Unauthorized`: usuario nao autenticado.
- `404 Not Found`: usuario autenticado nao encontrado no banco.

## Implementation Phases

### Phase 1: Tracer Bullet — Escolher e persistir um avatar

**Goal**: O usuario escolhe um avatar na aba `Conta`, salva a escolha e continua vendo esse avatar apos recarregar a aplicacao.

**Vertical slice**: seletor em `InformacoesConta.vue` -> servico/store de usuario -> `PUT /api/User/avatar` -> `ServiceUsuario` -> entidade e repositorio MongoDB -> `GET /api/User` -> avatar exibido no `MainLayout`.

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| AVTR-P1-01 | Adicionar `AvatarId` a `Domain/Usuario/Entity/Usuario.cs`, com constante/default `avatar-01` e metodo de dominio que aceite apenas IDs do catalogo permitido. | @Usuario | 1h |
| AVTR-P1-02 | Configurar `UsuarioMapping.cs` com default `avatar-01`, garantindo leitura transparente dos documentos legados sem migracao obrigatoria. | @Usuario | 30min |
| AVTR-P1-03 | Adicionar `AvatarId` ao `UsuarioDTO` e criar DTO especifico para atualizacao do avatar. | @Usuario | 30min |
| AVTR-P1-04 | Implementar `IServiceUsuario.AtualizarAvatarAsync`, buscando sempre por `IUsuarioLogado.Id`, validando no dominio e persistindo pelo repositorio existente. | @Usuario | 1h |
| AVTR-P1-05 | Expor `PUT /api/User/avatar` no grupo protegido de usuario e retornar o avatar salvo ou erros pelo `Result Pattern` existente. | @Usuario | 45min |
| AVTR-P1-06 | Criar no front-end o catalogo tipado de avatares, fallback e funcao que resolve `avatarId` para `/avatars/<id>.svg`. | @Usuario | 45min |
| AVTR-P1-07 | Estender `UserEmail-Store.ts` com estado e acoes para `avatarId`, mantendo `avatar-01` como valor inicial seguro. | @Usuario | 30min |
| AVTR-P1-08 | Criar servico de usuario para obter o perfil e atualizar o avatar usando a instancia autenticada de Axios. | @Usuario | 45min |
| AVTR-P1-09 | Adicionar uma primeira opcao selecionavel em `InformacoesConta.vue`, salvar pela API e atualizar a store somente apos sucesso. | @Usuario | 1h |
| AVTR-P1-10 | Fazer `MainLayout.vue` carregar o perfil autenticado na montagem e renderizar o avatar da store no cabecalho/menu e drawer. | @Usuario | 1h |

**Testing**:

- Back-end: validar ID permitido, ID invalido e usuario inexistente.
- API manual: executar `GET /api/User`, atualizar para outro ID pelo `PUT` e consultar novamente.
- Front-end manual: selecionar o avatar, fechar configuracoes, confirmar atualizacao imediata e recarregar a pagina.
- Seguranca: confirmar que o endpoint nao aceita `usuarioId` e altera somente `IUsuarioLogado.Id`.
- Build: executar `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`, `npm run lint` e `npm run build`.

**Acceptance Criteria**:

- [ ] Um usuario autenticado consegue salvar um avatar permitido.
- [ ] A escolha aparece imediatamente no proprio layout e permanece apos recarregar.
- [ ] Um ID fora do catalogo retorna erro e nao altera o documento.
- [ ] Usuarios sem `AvatarId` recebem visualmente `avatar-01`.
- [ ] Nenhum contrato de login, refresh token ou compartilhamento e alterado.

**Dependencies**: Definicao e disponibilizacao de pelo menos um arquivo de avatar final para provar o fluxo completo.

---

### Phase 2: Catalogo completo e experiencia de selecao

**Goal**: O usuario escolhe com clareza entre pelo menos oito avatares e ve a mesma representacao em todas as superficies do proprio perfil.

**Vertical slice**: oito assets versionados -> catalogo tipado -> grade responsiva e acessivel -> store reativa -> cabecalho, menu lateral e configuracoes.

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| AVTR-P2-01 | Adicionar no minimo oito SVGs otimizados em `FinanMap-Front-End/public/avatars/`, com IDs e nomes acessiveis correspondentes ao catalogo permitido no back-end. | @Usuario | 2h |
| AVTR-P2-02 | Completar e documentar o catalogo compartilhado por convencao (`avatar-01` a `avatar-08`) no front-end e no back-end, evitando caminhos arbitrarios vindos da API. | @Usuario | 45min |
| AVTR-P2-03 | Criar componente reutilizavel `UserAvatar.vue` que receba `avatarId`, tamanho e texto alternativo, aplicando fallback local para IDs desconhecidos. | @Usuario | 1h |
| AVTR-P2-04 | Substituir o icone `person`, os usos fixos de `/avatar.svg` e a inicial da conta pelo componente reutilizavel apenas nas superficies do proprio usuario. | @Usuario | 1h |
| AVTR-P2-05 | Implementar grade de selecao responsiva em `InformacoesConta.vue`, com estado selecionado, foco visivel, navegacao por teclado e nome acessivel para cada opcao. | @Usuario | 2h |
| AVTR-P2-06 | Adicionar estados de salvamento, sucesso e falha; durante falha, manter a escolha persistida anterior e comunicar o erro sem deixar layout/store divergentes. | @Usuario | 1h |
| AVTR-P2-07 | Garantir que reabrir configuracoes ou alternar entre abas nao faca consultas duplicadas desnecessarias nem perca o estado atual. | @Usuario | 45min |

**Testing**:

- Testar selecao de cada uma das oito opcoes e persistencia apos recarga.
- Simular falha da API e confirmar que o avatar anterior continua ativo.
- Testar resposta com `avatarId` ausente ou desconhecido e confirmar fallback.
- Validar desktop, 320px, tablet, light mode e dark mode.
- Navegar pela grade apenas com teclado e confirmar foco/estado selecionado perceptiveis.
- Confirmar que telas de compartilhamento continuam usando os icones atuais.

**Acceptance Criteria**:

- [ ] Pelo menos oito opcoes distintas ficam disponiveis na aba `Conta`.
- [ ] A opcao atual e identificavel visualmente e por tecnologia assistiva.
- [ ] O mesmo avatar aparece no cabecalho, menu de conta, drawer e cabecalho da conta.
- [ ] Uma falha ao salvar nao deixa store, tela e banco com valores diferentes.
- [ ] Nenhum avatar de outro usuario e exibido ou alterado.
- [ ] O seletor funciona sem overflow em 320px e nos modos claro/escuro.

**Dependencies**: Phase 1 concluida; conjunto final de avatares aprovado.

---

### Phase 3: Compatibilidade, verificacao e liberacao

**Goal**: Liberar a preferencia de avatar com compatibilidade para usuarios existentes e baixo risco operacional.

**Vertical slice**: documentos legados -> mapping/fallback -> API publicada -> bundle front-end -> verificacao em ambiente de homologacao/producao.

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| AVTR-P3-01 | Verificar no MongoDB uma amostra de usuarios legados e confirmar que ausencia de `AvatarId` nao causa erro de desserializacao nem sobrescreve outros campos ao salvar. | @Usuario | 45min |
| AVTR-P3-02 | Adicionar testes automatizados focados para catalogo/validacao e atualizacao do proprio usuario, caso seja criado o primeiro projeto de testes do back-end nesta entrega. | @Usuario | 2h |
| AVTR-P3-03 | Executar regressao autenticada: login, refresh, abertura de configuracoes, troca de avatar, recarga, logout/login e modo compartilhado. | @Usuario | 1.5h |
| AVTR-P3-04 | Executar builds finais, validar que os oito assets entram no bundle/deploy e registrar evidencias da entrega. | @Usuario | 1h |
| AVTR-P3-05 | Publicar back-end antes do front-end para que o novo contrato ja esteja disponivel quando o seletor chegar aos usuarios. | @Usuario | 30min |

**Testing**:

- Back-end: `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln`.
- Front-end: `npm run lint`, `npm test` e `npm run build`.
- Homologacao: usuario legado, usuario com avatar salvo e tentativa de ID invalido.
- Regressao: confirmar que nome/e-mail, notificacoes, login, refresh e compartilhamento permanecem funcionais.

**Acceptance Criteria**:

- [ ] Usuarios legados acessam a aplicacao sem migracao manual e veem o avatar padrao.
- [ ] O back-end e publicado antes ou junto do front-end sem janela de incompatibilidade.
- [ ] Builds e verificacoes existentes passam sem novos erros.
- [ ] Login, refresh token e compartilhamento nao apresentam regressao.
- [ ] Os oito assets sao servidos corretamente no ambiente publicado.

**Dependencies**: Phases 1 e 2 concluidas; ambiente autenticado e acesso de leitura ao MongoDB para validacao.

## Milestones

| Milestone | Target Date | Description |
|-----------|-------------|-------------|
| Fluxo ponta a ponta validavel | Sem deadline | Avatar permitido persistido e exibido apos recarga |
| Experiencia completa | Sem deadline | Oito opcoes responsivas refletidas nas superficies do proprio perfil |
| Pronto para producao | Sem deadline | Compatibilidade legada, regressao e publicacao validadas |

## Dependencies

| Dependency | Type | Owner | Status | Risk if Delayed |
|------------|------|-------|--------|-----------------|
| Conjunto final de oito avatares | Product/Design | @Usuario | Pendente | Impede finalizar o catalogo e validar consistencia visual |
| `GET /api/User` e `IUsuarioLogado` | Internal | @Usuario | Disponivel | Sao a base para carregar e alterar somente o proprio perfil |
| MongoDB e `UsuarioMapping` | Technical | @Usuario | Disponivel | Default incorreto pode afetar documentos legados |
| Ambiente autenticado para regressao | Technical | @Usuario | A confirmar | Sem ele, persistencia e seguranca ficam validadas apenas parcialmente |
| Publicacao coordenada de back-end e front-end | Operational | @Usuario | Pendente | Front-end publicado primeiro pode chamar endpoint ainda indisponivel |

## Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Front-end e back-end divergirem sobre IDs permitidos | Alto | Media | Usar convencao simples e documentada, testes dos IDs e fallback apenas para leitura |
| Documento legado perder campos durante `ReplaceOneAsync` | Alto | Baixa | Validar mapping/default e testar usuario legado antes da liberacao |
| Store e banco divergirem quando o salvamento falhar | Medio | Media | Atualizar store somente apos sucesso ou reverter explicitamente para o valor persistido |
| Assets ausentes ou renomeados quebrarem imagens em producao | Medio | Media | Resolver caminhos por catalogo fechado e verificar todos os arquivos no build/publicacao |
| Nova consulta de perfil gerar chamadas duplicadas | Baixo | Media | Centralizar carregamento na store/layout e reutilizar o estado em `InformacoesConta.vue` |
| Endpoint permitir alteracao de outro usuario | Alto | Baixa | Nao aceitar ID de usuario e usar exclusivamente `IUsuarioLogado.Id` |

## Testing Strategy

Os projetos atualmente nao possuem suites automatizadas completas. A entrega deve combinar validacao automatizavel de baixo custo, builds e regressao manual autenticada.

### Back-end

- Testes de dominio para ID permitido, vazio e desconhecido.
- Teste de servico para garantir uso do usuario autenticado e persistencia do novo valor.
- Teste de contrato/manual do `GET /api/User` e `PUT /api/User/avatar`.
- Build completo da solucao.

### Front-end

- Lint, teste existente e build.
- Validacao do helper de fallback e resolucao de caminho, caso seja adicionada infraestrutura de testes.
- Teste funcional das oito opcoes, falha de API e recarga.
- Teste visual em desktop/mobile e light/dark.
- Teste de acessibilidade por teclado e nomes acessiveis.

### Test Data

- Usuario legado sem `AvatarId`.
- Usuario com `avatar-01`.
- Usuario com outro avatar permitido.
- Resposta simulada com ID desconhecido.
- Requisicao de atualizacao com ID invalido.

## Rollback Plan

### Deployment Strategy

1. Publicar o back-end com campo opcional/default, retorno ampliado e novo endpoint.
2. Validar que clientes antigos continuam funcionando.
3. Publicar o front-end com seletor e novos assets.

### Rollback Triggers

- Usuarios nao conseguem abrir o perfil ou autenticar.
- Documentos de usuario perdem dados apos trocar o avatar.
- Imagens nao carregam de forma generalizada.
- Troca de avatar afeta outro usuario ou contexto compartilhado.

### Rollback Steps

1. Reverter o front-end para remover o seletor e restaurar o avatar fixo.
2. Manter temporariamente o campo e endpoint no back-end, pois sao aditivos e clientes antigos os ignoram.
3. Se o problema estiver na persistencia, desabilitar/reverter o endpoint de atualizacao antes de qualquer ajuste nos documentos.
4. Nao remover `AvatarId` dos documentos ja gravados; ele pode permanecer sem afetar clientes antigos.
5. Corrigir, repetir os cenarios de usuario legado e seguranca e republicar back-end antes do front-end.

## Validation Checklist

- [x] Technical design referenced: nao fornecido
- [x] Phase 1 entrega valor ponta a ponta
- [x] Todas as fases possuem goal, vertical slice, tasks, testing, acceptance criteria e dependencies
- [x] IDs seguem o formato `AVTR-PN-NN` e sao unicos
- [x] Testes estao incorporados em cada fase
- [x] Nenhuma fase e apenas setup ou infraestrutura
- [x] Milestones definidos
- [x] Dependencies e riscos documentados
- [x] Testing Strategy incluida
- [x] Rollback Plan incluido
