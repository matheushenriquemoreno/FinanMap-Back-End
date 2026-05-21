using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Email.DTOs;
using Application.Email.Htmls;
using Application.Email.Interfaces;
using Domain.Enum;

namespace Application.Email.Implementacoes;

public class CustoFixoEmailService : ICustoFixoEmailService
{
    private readonly IProvedorEmail _provedorEmail;

    public CustoFixoEmailService(IProvedorEmail provedorEmail)
    {
        _provedorEmail = provedorEmail;
    }

    public async Task<Result> EnviarLembreteAsync(string email, string nomeUsuario, List<CustoFixoLembreteItem> itens, TipoLembrete tipo)
    {
        var assunto = tipo == TipoLembrete.DiaDoVencimento
            ? "Hoje é dia de vencimento!"
            : "Seus vencimentos estão chegando!";

        var html = CustoFixoLembreteHtmls.ObterHtmlLembrete(nomeUsuario, itens, tipo);

        return await _provedorEmail.EnviarEmail(assunto, html, email);
    }
}
