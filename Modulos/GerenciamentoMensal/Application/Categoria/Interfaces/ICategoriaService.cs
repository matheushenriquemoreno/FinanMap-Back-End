using Application.DTOs;
using Application.Shared.Interfaces.Service;
using Domain.Entity;
using Domain.Enum;

namespace Application.Interfaces
{
    public interface ICategoriaService :
        IServiceBase<Categoria, CreateCategoriaDTO, UpdateCategoriaDTO, ResultCategoriaDTO>
    {
        Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string descricao);
    }
}
