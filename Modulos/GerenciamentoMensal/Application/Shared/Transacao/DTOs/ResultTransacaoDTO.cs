using Application.Shared.Transacao.DTOs;

namespace Application.Shared.DTOs;

public class ResultTransacaoDTO
{
    public string Id { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Descricao { get; set; }
    public decimal Valor { get; set; }
    public string CategoriaNome { get; set; }
    public string CategoriaId { get; set; }

    public static implicit operator ResultTransacaoDTO(Domain.Entity.Transacao transacao)
    {
        return new ResultTransacaoDTO
        {
            Ano = transacao.Ano,
            Mes = transacao.Mes,
            CategoriaNome = transacao.Categoria?.Nome,
            CategoriaId = transacao.Categoria?.Id,
            Id = transacao.Id,
            Descricao = transacao.Descricao,
            Valor = transacao.Valor,
        };
    }

    public TransacaoReportDTO ReportAcumulado { get; set; }
}
