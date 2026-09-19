using Domain.Exceptions;
using Domain.Validator;

namespace Domain.Entity;

public class CompraPlanejada : EntityBase
{
    public string UsuarioId { get; private set; }
    public string Nome { get; private set; }
    public decimal ValorEstimado { get; private set; }
    public PrioridadeCompraPlanejada Prioridade { get; private set; }
    public string Descricao { get; private set; }
    public List<LinkLojaCompraPlanejada> LinksLojas { get; private set; } = [];
    public EstadoCompraPlanejada Estado { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public decimal? ValorReal { get; private set; }
    public DateTime? DataCompra { get; private set; }
    public string DespesaId { get; private set; }

    protected CompraPlanejada()
    {
    }

    public CompraPlanejada(
        string usuarioId,
        string nome,
        decimal valorEstimado,
        PrioridadeCompraPlanejada prioridade,
        string descricao = null,
        IEnumerable<LinkLojaCompraPlanejada> linksLojas = null)
    {
        UsuarioId = usuarioId?.Trim();
        Nome = nome?.Trim();
        ValorEstimado = valorEstimado;
        Prioridade = prioridade;
        Descricao = descricao?.Trim();
        LinksLojas = linksLojas?.ToList() ?? [];
        Estado = EstadoCompraPlanejada.Pendente;
        DataCriacao = DateTime.UtcNow;

        ValidarDados();
    }

    public void Atualizar(
        string nome,
        decimal valorEstimado,
        PrioridadeCompraPlanejada prioridade,
        string descricao,
        IEnumerable<LinkLojaCompraPlanejada> linksLojas)
    {
        if (Estado != EstadoCompraPlanejada.Pendente)
            throw new DomainValidatorException("Somente itens pendentes podem ser alterados.");

        var novoNome = nome?.Trim();
        var novosLinks = linksLojas?.ToList() ?? [];

        ValidarDados(novoNome, valorEstimado, prioridade, novosLinks);

        Nome = novoNome;
        ValorEstimado = valorEstimado;
        Prioridade = prioridade;
        Descricao = descricao?.Trim();
        LinksLojas = novosLinks;
    }

    public void MarcarComoComprado(decimal valorReal, DateTime dataCompra)
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => Estado != EstadoCompraPlanejada.Pendente, "Item já está comprado.");
        validator.Validar(() => valorReal <= 0, "Valor real deve ser maior que zero.");
        validator.Validar(() => dataCompra.Date > DateTime.UtcNow.Date, "Data da compra não pode ser futura.");
        validator.LancarExceptionSePossuiErro();

        Estado = EstadoCompraPlanejada.Comprado;
        ValorReal = valorReal;
        DataCompra = dataCompra.Date;
        DespesaId = null;
    }

    public void VincularDespesa(string despesaId)
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => Estado != EstadoCompraPlanejada.Comprado, "Somente itens comprados podem ter despesa vinculada.");
        validator.Validar(() => string.IsNullOrWhiteSpace(despesaId), "Identificador da despesa é obrigatório.");
        validator.Validar(() => !string.IsNullOrWhiteSpace(DespesaId), "Item já possui uma despesa vinculada.");
        validator.LancarExceptionSePossuiErro();

        DespesaId = despesaId;
    }

    public void RemoverVinculoDespesa()
    {
        DespesaId = null;
    }

    public void ReverterCompra()
    {
        if (Estado != EstadoCompraPlanejada.Comprado)
            throw new DomainValidatorException("Somente itens comprados podem ser revertidos.");

        Estado = EstadoCompraPlanejada.Pendente;
        ValorReal = null;
        DataCompra = null;
        DespesaId = null;
    }

    private void ValidarDados()
        => ValidarDados(Nome, ValorEstimado, Prioridade, LinksLojas);

    private void ValidarDados(
        string nome,
        decimal valorEstimado,
        PrioridadeCompraPlanejada prioridade,
        IEnumerable<LinkLojaCompraPlanejada> linksLojas)
    {
        var validator = DomainValidator.Create();

        validator.Validar(() => string.IsNullOrWhiteSpace(UsuarioId), "Id do proprietário é obrigatório.");
        validator.Validar(() => string.IsNullOrWhiteSpace(nome), "Nome da compra é obrigatório.");
        validator.Validar(() => valorEstimado <= 0, "Valor estimado deve ser maior que zero.");
        validator.Validar(
            () => !System.Enum.IsDefined(typeof(PrioridadeCompraPlanejada), prioridade),
            "Prioridade da compra é inválida.");
        validator.Validar(
            () => linksLojas.Any(link => link is null),
            "A lista de links da loja contém item inválido.");

        validator.LancarExceptionSePossuiErro();
    }
}
