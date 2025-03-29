using Domain.Entity;
using Domain.Enum;
using Mapster;

namespace Application.DTOs;

public class ResultCategoriaDTO
{
    public string Id { get; set; }
    public string Nome { get; set; }

    public TipoCategoria Tipo { get; set; }

    public static ResultCategoriaDTO Mapear(Categoria entity)
    {
        return entity.Adapt<ResultCategoriaDTO>();
    }
}
