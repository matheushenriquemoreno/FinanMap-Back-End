using System.Linq.Expressions;
using Application.DTOs;
using Application.Implementacoes;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Exceptions;
using Domain.Login.Interfaces;
using Domain.Repository;
using Infra.Data.Mongo.Mappings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Tests;

public class UsuarioAvatarTests
{
    [Theory]
    [InlineData("avatar-01")]
    [InlineData("avatar-08")]
    public void AtualizarAvatar_AceitaIdentificadorDoCatalogo(string avatarId)
    {
        var usuario = CriarUsuario("usuario-id");

        usuario.AtualizarAvatar(avatarId);

        Assert.Equal(avatarId, usuario.AvatarId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("avatar-09")]
    [InlineData("../avatar-01")]
    public void AtualizarAvatar_RejeitaIdentificadorForaDoCatalogo(string avatarId)
    {
        var usuario = CriarUsuario("usuario-id");

        Assert.Throws<DomainValidatorException>(() => usuario.AtualizarAvatar(avatarId));
        Assert.Equal(Usuario.AvatarPadrao, usuario.AvatarId);
    }

    [Fact]
    public void DocumentoLegadoSemAvatar_UsaPadraoEPreservaCamposAoSerializar()
    {
        new UsuarioMapping().RegisterMap(null!);
        var documentoLegado = new BsonDocument
        {
            { "Nome", "Usuário legado" },
            { "Email", "legado@finanmap.com" },
            { "ReceberNotificacoesCustosFixos", false }
        };

        var usuario = BsonSerializer.Deserialize<Usuario>(documentoLegado);
        usuario.AtualizarAvatar("avatar-03");
        var documentoAtualizado = usuario.ToBsonDocument();

        Assert.Equal(Usuario.AvatarPadrao, BsonSerializer.Deserialize<Usuario>(documentoLegado).AvatarId);
        Assert.Equal("Usuário legado", documentoAtualizado["Nome"].AsString);
        Assert.Equal("legado@finanmap.com", documentoAtualizado["Email"].AsString);
        Assert.False(documentoAtualizado["ReceberNotificacoesCustosFixos"].AsBoolean);
        Assert.Equal("avatar-03", documentoAtualizado["AvatarId"].AsString);
    }

    [Fact]
    public async Task AtualizarAvatar_UsaUsuarioAutenticadoMesmoEmModoCompartilhado()
    {
        var usuarioAutenticado = CriarUsuario("usuario-autenticado");
        var proprietarioCompartilhado = CriarUsuario("proprietario-compartilhado");
        var usuarioLogado = new UsuarioLogadoFake(usuarioAutenticado, proprietarioCompartilhado);
        var repositorio = new UsuarioRepositoryFake(usuarioAutenticado);
        var service = new ServiceUsuario(usuarioLogado, repositorio);

        var resultado = await service.AtualizarAvatarAsync(new AtualizarAvatarDTO { AvatarId = "avatar-04" });

        Assert.True(resultado.IsSucess);
        Assert.Equal("usuario-autenticado", repositorio.IdConsultado);
        Assert.Same(usuarioAutenticado, repositorio.UsuarioAtualizado);
        Assert.Equal("avatar-04", resultado.Value.AvatarId);
        Assert.Equal(Usuario.AvatarPadrao, proprietarioCompartilhado.AvatarId);
    }

    [Fact]
    public async Task AtualizarAvatarInvalido_NaoPersisteAlteracao()
    {
        var usuario = CriarUsuario("usuario-id");
        var repositorio = new UsuarioRepositoryFake(usuario);
        var service = new ServiceUsuario(new UsuarioLogadoFake(usuario), repositorio);

        var resultado = await service.AtualizarAvatarAsync(new AtualizarAvatarDTO { AvatarId = "avatar-09" });

        Assert.True(resultado.IsFailure);
        Assert.Null(repositorio.UsuarioAtualizado);
        Assert.Equal(Usuario.AvatarPadrao, usuario.AvatarId);
    }

    private static Usuario CriarUsuario(string id) =>
        new("Usuário Teste", $"{id}@finanmap.com") { Id = id };

    private sealed class UsuarioLogadoFake(Usuario usuario, Usuario? usuarioContexto = null) : IUsuarioLogado
    {
        public string Id => usuario.Id;
        public Usuario Usuario => usuario;
        public string IdContextoDados => UsuarioContextoDados.Id;
        public Usuario UsuarioContextoDados => usuarioContexto ?? usuario;
        public bool EmModoCompartilhado => usuarioContexto is not null;
        public NivelPermissao? PermissaoAtual => EmModoCompartilhado ? NivelPermissao.Editar : null;
    }

    private sealed class UsuarioRepositoryFake(Usuario usuario) : IUsuarioRepository
    {
        public string? IdConsultado { get; private set; }
        public Usuario? UsuarioAtualizado { get; private set; }

        public Task<Usuario> GetById(string id)
        {
            IdConsultado = id;
            return Task.FromResult(usuario);
        }

        public Task<Usuario> Update(Usuario entity)
        {
            UsuarioAtualizado = entity;
            return Task.FromResult(entity);
        }

        public Task<Usuario> Add(Usuario entity) => throw new NotSupportedException();
        public Task<List<Usuario>> Add(List<Usuario> entity) => throw new NotSupportedException();
        public Task Delete(Usuario entity) => throw new NotSupportedException();
        public Task<List<Usuario>> GetByIds(List<string> ids) => throw new NotSupportedException();
        public Task<IEnumerable<Usuario>> GetWhere(Expression<Func<Usuario, bool>> filtro) =>
            throw new NotSupportedException();
        public Task<Usuario> GetByEmail(string email) => throw new NotSupportedException();
        public Task<List<string>> FiltrarUsuariosComNotificacaoAtiva(List<string> usuarioIds) =>
            throw new NotSupportedException();
    }
}
