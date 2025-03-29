using Domain.Entity;

namespace Domain.Login.Interfaces;

public interface IUsuarioLogado
{
    string Id { get; }
    Usuario Usuario { get; }
}
