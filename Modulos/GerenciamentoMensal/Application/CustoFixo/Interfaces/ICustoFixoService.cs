using Application.CustoFixo.DTOs;
using Application.Shared.Interfaces.Service;

namespace Application.CustoFixo.Interfaces;

public interface ICustoFixoService :
    IServiceBase<Domain.Entity.CustoFixo, CreateCustoFixoDTO, UpdateCustoFixoDTO, CustoFixoResponseDTO>
{
    Task<Result<List<CustoFixoResponseDTO>>> Listar();
}
