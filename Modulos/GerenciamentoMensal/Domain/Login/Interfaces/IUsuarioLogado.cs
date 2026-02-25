using Domain.Compartilhamento.Entity;
using Domain.Entity;

namespace Domain.Login.Interfaces;

public interface IUsuarioLogado
{
    /// <summary>
    /// ID do usuário autenticado (sempre do JWT)
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Usuário autenticado
    /// </summary>
    Usuario Usuario { get; }

    /// <summary>
    /// ID do contexto de dados — será o ProprietarioId se estiver em modo compartilhado,
    /// ou o próprio Id se estiver vendo seus próprios dados
    /// </summary>
    string IdContextoDados { get; }

    /// <summary>
    /// Usuário do contexto de dados — será o Proprietário se estiver em modo compartilhado,
    /// ou o próprio Usuário autenticado se estiver vendo seus próprios dados
    /// </summary>
    Usuario UsuarioContextoDados { get; }

    /// <summary>
    /// Indica se o usuário está acessando dados de outro usuário
    /// </summary>
    bool EmModoCompartilhado { get; }

    /// <summary>
    /// Nível de permissão no contexto compartilhado (null se não estiver em modo compartilhado)
    /// </summary>
    NivelPermissao? PermissaoAtual { get; }
}
