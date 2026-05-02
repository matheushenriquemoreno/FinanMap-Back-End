using Application.Email.Interfaces;
using Infra.Configure.Env;
using Microsoft.Extensions.Logging;
using Resend;

namespace Infra.Email;

public class ResendEmailProvedor : IProvedorEmail
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailProvedor> _logger;

    public ResendEmailProvedor(IResend resend, ILogger<ResendEmailProvedor> logger)
    {
        _resend = resend;
        _logger = logger;
    }

    public async Task<Result> EnviarEmail(string assunto, string conteudoHTML, string email)
    {
        try
        {
            _logger.LogInformation("Provedor: {Provedor} Enviando e-mail para {Email}",
                nameof(ResendEmailProvedor), email);

            var message = new EmailMessage
            {
                From = EmailSettings.EmailRemetente,
                Subject = assunto,
                HtmlBody = conteudoHTML,
            };
            message.To.Add(email);

            await _resend.EmailSendAsync(message);

            _logger.LogInformation("Provedor: {Provedor} e-mail enviado com sucesso! para: {Email}",
                nameof(ResendEmailProvedor), email);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provedor: {Provedor} erro ao enviar e-mail para {Email}",
                nameof(ResendEmailProvedor), email);
            return Result.Failure(Error.Exception(ex));
        }
    }
}
