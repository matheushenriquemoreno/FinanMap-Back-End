using Domain.Exceptions;
using Domain.Validator;

namespace Domain.Entity;

public class MetaFinanceira : EntityBase
{
    public string Nome { get; private set; }
    public decimal ValorAlvo { get; private set; }
    public DateTime DataLimite { get; private set; }
    public CategoriaIconeMeta Categoria { get; private set; }
    public string UsuarioId { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public List<Contribuicao> Contribuicoes { get; private set; } = new();

    // Campos calculados (não persistidos, preenchidos no Service/DTO)
    public decimal ValorAtual => Contribuicoes.Sum(c => c.Valor);
    public decimal PercentualProgresso => ValorAlvo > 0 ? Math.Min((ValorAtual / ValorAlvo) * 100, 100) : 0;
    public bool Concluida => ValorAtual >= ValorAlvo;

    protected MetaFinanceira() { }

    public MetaFinanceira(string nome, decimal valorAlvo, DateTime dataLimite,
                          CategoriaIconeMeta categoria, Usuario usuario)
    {
        Nome = nome;
        ValorAlvo = valorAlvo;
        DataLimite = dataLimite;
        Categoria = categoria;
        UsuarioId = usuario.Id;
        DataCriacao = DateTime.Now;
        ValidarDados();
    }

    public void Atualizar(string nome, decimal valorAlvo, DateTime dataLimite,
                          CategoriaIconeMeta categoria)
    {
        Nome = nome;
        ValorAlvo = valorAlvo;
        DataLimite = dataLimite;
        Categoria = categoria;
        ValidarDados();
    }

    public NotificacaoMeta? AdicionarContribuicao(decimal valor, DateTime data,
                                                   string? investimentoId = null,
                                                   string? nomeInvestimento = null)
    {
        var validator = DomainValidator.Create();
        validator.Validar(() => valor <= 0, "O valor da contribuição deve ser positivo.");
        validator.LancarExceptionSePossuiErro();

        decimal percentualAntes = PercentualProgresso;

        var contribuicao = new Contribuicao(valor, data)
        {
            InvestimentoId = investimentoId,
            NomeInvestimento = nomeInvestimento,
            Origem = investimentoId != null
                ? OrigemContribuicao.Investimento
                : OrigemContribuicao.Manual
        };

        Contribuicoes.Add(contribuicao);

        decimal percentualDepois = PercentualProgresso;

        // Gerar notificação de marco, se aplicável
        return GerarNotificacaoMarco(percentualAntes, percentualDepois);
    }

    public void RemoverContribuicao(string contribuicaoId)
    {
        var contribuicao = Contribuicoes.FirstOrDefault(c => c.Id == contribuicaoId);
        if (contribuicao == null)
            throw new DomainValidatorException("Contribuição não encontrada.");
        Contribuicoes.Remove(contribuicao);
    }

    private NotificacaoMeta? GerarNotificacaoMarco(decimal percentualAntes, decimal percentualDepois)
    {
        if (percentualAntes < 100 && percentualDepois >= 100)
            return new NotificacaoMeta(TipoNotificacaoMeta.MetaAlcancada, Nome, ValorAlvo, ValorAtual);

        if (percentualAntes < 80 && percentualDepois >= 80)
            return new NotificacaoMeta(TipoNotificacaoMeta.QuaseLa, Nome, ValorAlvo, ValorAtual);

        if (percentualAntes < 50 && percentualDepois >= 50)
            return new NotificacaoMeta(TipoNotificacaoMeta.MetadeCaminho, Nome, ValorAlvo, ValorAtual);

        return null;
    }

    private void ValidarDados()
    {
        var validator = DomainValidator.Create();
        validator.Validar(() => string.IsNullOrWhiteSpace(Nome), "O nome da meta é obrigatório.");
        validator.Validar(() => ValorAlvo <= 0, "O valor alvo deve ser positivo.");
        validator.Validar(() => DataLimite < DateTime.Today, "A data limite não pode ser no passado.");
        validator.LancarExceptionSePossuiErro();
    }
}
