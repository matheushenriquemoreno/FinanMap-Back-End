# Release Checklist: selecao-avatar-usuario

Data da verificacao local: 2026-06-06

## Evidencias automatizadas

- [x] `dotnet test Modulos/GerenciamentoMensal/FinancasPessoais.sln`
  - 8 testes aprovados.
  - Cobre IDs permitidos e invalidos, fallback de documento legado, preservacao de campos,
    atualizacao do usuario autenticado em modo compartilhado e ausencia de persistencia invalida.
- [x] `dotnet build Modulos/GerenciamentoMensal/FinancasPessoais.sln --no-restore`
  - Build aprovado com 3 avisos preexistentes no projeto `WebApi`.
- [x] `dotnet format Modulos/GerenciamentoMensal/FinancasPessoais.sln --verify-no-changes --no-restore --include Modulos/GerenciamentoMensal/Tests`
  - Arquivos novos aprovados.
- [x] `npm run lint`
- [x] `npm test`
- [x] `npm run build`
- [x] `npm run test:avatar:release`
  - Confirma os oito SVGs em `dist/spa/avatars`.
  - Confirma que o contrato de autenticacao e a tela de compartilhamento nao incorporaram avatar.

## Verificacoes pendentes de ambiente

- [ ] Consultar uma amostra de usuarios reais sem `AvatarId` no MongoDB de homologacao.
- [ ] Confirmar que salvar avatar em usuario legado nao sobrescreve campos no documento real.
- [ ] Executar login, refresh, configuracoes, troca de avatar, recarga e logout/login autenticados.
- [ ] Executar o fluxo em modo compartilhado com usuario convidado.
- [ ] Publicar o back-end e validar `GET /api/User` e `PUT /api/User/avatar`.
- [ ] Publicar o front-end somente depois da validacao do back-end.
- [ ] Confirmar HTTP 200 para `/avatars/avatar-01.svg` ate `/avatars/avatar-08.svg` no ambiente publicado.

## Observacoes

- O formatador global ainda encontra tres erros de whitespace preexistentes em
  `Application/CustoFixo/Service/CustoFixoLembreteService.cs`.
- A publicacao nao foi executada localmente porque exige acesso ao ambiente de deploy e deve
  respeitar a ordem back-end antes do front-end.
