namespace Application.CustoFixo.DTOs;

public class CustoFixoResponseDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }
    public int DiaVencimento { get; set; }
    public string CategoriaId { get; set; }
    public bool Ativo { get; set; }

    public static CustoFixoResponseDTO Mapear(Domain.Entity.CustoFixo entity)
    {
        return new CustoFixoResponseDTO
        {
            Id = entity.Id,
            Nome = entity.Nome,
            DiaVencimento = entity.DiaVencimento,
            CategoriaId = entity.CategoriaId,
            Ativo = entity.Ativo
        };
    }
}
