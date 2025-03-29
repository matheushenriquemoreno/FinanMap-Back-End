using Domain.Login.Entity;

namespace Application.Email.Interfaces;

public interface IUsuarioEmailService
{
    Task<Result> EnviarEmailParaLogin(bool primeiroLogin, string email, CodigoLogin codigo);
}
