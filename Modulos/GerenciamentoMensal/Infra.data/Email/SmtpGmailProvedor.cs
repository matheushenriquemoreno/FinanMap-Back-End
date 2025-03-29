using System.Net;
using System.Net.Mail;
using Application.Email.Interfaces;
using Infra.Configure.Env;
using Microsoft.Extensions.Logging;

namespace Infra.Email;

public class SmtpGmailProvedor : IProvedorEmail
{
    private readonly ILogger<SmtpGmailProvedor> logger;

    public SmtpGmailProvedor(ILogger<SmtpGmailProvedor> logger)
    {
        this.logger = logger;
    }

    public async Task<Result> EnviarEmail(string assunto, string conteudoHTML, string email)
    {
        try
        {
            logger.LogInformation("Provedor: {0} Enviando e-mail para {1}", nameof(SmtpGmailProvedor), email);

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(EmailSettings.Email, EmailSettings.Password),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(email, "FinamMap"),
                Subject = assunto,
                Body = conteudoHTML,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);

            logger.LogInformation("Provedor: {0} e-mail enviado com sucesso! para: {1}", nameof(SmtpGmailProvedor), email);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Provedor: {0} um erro ao Não foi possivel enviar e-mail para {1}", nameof(SmtpGmailProvedor), email);
            return Result.Failure(Error.Exception(ex));
        }
    }
}
