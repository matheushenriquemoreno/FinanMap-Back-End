namespace Application.Email.Interfaces;

public interface IProvedorEmail
{
    Task<Result> EnviarEmail(string assunto, string conteudoHTML, string email);
}
