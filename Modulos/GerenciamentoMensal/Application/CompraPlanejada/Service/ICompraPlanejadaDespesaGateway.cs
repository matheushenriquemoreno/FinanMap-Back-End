using Application.DTOs;

namespace Application.CompraPlanejada.Service;

public interface ICompraPlanejadaDespesaGateway
{
    Task<Result<string>> Criar(CreateDespesaDTO dto);
    Task<Result> Excluir(string id);
}
