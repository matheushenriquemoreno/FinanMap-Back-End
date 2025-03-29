using Domain.Entity;

namespace Application.Login.Interfaces;

public interface IServiceJWT
{
    string CriarToken(Usuario user);
}
