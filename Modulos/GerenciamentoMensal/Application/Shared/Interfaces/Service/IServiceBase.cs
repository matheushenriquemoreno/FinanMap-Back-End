using Domain.Entity;

namespace Application.Shared.Interfaces.Service;

public interface IServiceBase<TEntity, TCreateDTO, TUpdateDTO, TResultDTO>
    where TEntity : EntityBase
    where TCreateDTO : class
    where TUpdateDTO : class
    where TResultDTO : class
{
    Task<Result<TResultDTO>> Adicionar(TCreateDTO createDTO);
    Task<Result<TResultDTO>> Atualizar(TUpdateDTO updateDTO);
    Task<Result> Excluir(string id);
    Task<Result<TResultDTO>> ObterPeloID(string id);
}