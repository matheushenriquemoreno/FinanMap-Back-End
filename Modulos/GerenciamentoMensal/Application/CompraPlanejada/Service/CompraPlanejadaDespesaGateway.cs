using Application.DTOs;
using Application.Interfaces;

namespace Application.CompraPlanejada.Service;

public class CompraPlanejadaDespesaGateway : ICompraPlanejadaDespesaGateway
{
    private readonly IDespesaService _despesaService;

    public CompraPlanejadaDespesaGateway(IDespesaService despesaService)
    {
        _despesaService = despesaService;
    }

    public async Task<Result<string>> Criar(CreateDespesaDTO dto)
    {
        var resultado = await _despesaService.Adicionar(dto);

        return resultado.IsFailure
            ? Result.Failure<string>(resultado.Error)
            : Result.Success(resultado.Value.Id);
    }

    public Task<Result> Excluir(string id)
        => _despesaService.Excluir(id);
}
