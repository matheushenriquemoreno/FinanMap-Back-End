﻿using Application.Email.Htmls;
using Application.Email.Interfaces;
using Domain.Login.Entity;

namespace Application.Email.Implementacoes;

public class EmailService : IUsuarioEmailService
{
    private readonly IProvedorEmail _provedorEmail;

    public EmailService(IProvedorEmail provedorEmail)
    {
        _provedorEmail = provedorEmail;
    }

    public async Task<Result> EnviarEmailParaLogin(bool primeiroLogin, string email, CodigoLogin codigo)
    {
        var assunto = primeiroLogin ? "Seja bem vindo!" : $"Codigo de login: {codigo.Codigo}";

        var html = LoginHtmls.ObterHtmlLogin(codigo.Codigo, codigo.MinutosExpiracao);

        return await _provedorEmail.EnviarEmail(assunto, html, email);
    }
}
