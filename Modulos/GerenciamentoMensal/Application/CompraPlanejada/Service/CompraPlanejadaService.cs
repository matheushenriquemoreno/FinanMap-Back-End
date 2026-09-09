using Application.CompraPlanejada.DTOs;
using Application.CompraPlanejada.Interfaces;
using Application.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Exceptions;
using Domain.Login.Interfaces;
using Domain.Repository;

namespace Application.CompraPlanejada.Service;

public class CompraPlanejadaService : ICompraPlanejadaService
{
    private readonly ICompraPlanejadaRepository _repository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly ICompraPlanejadaDespesaGateway _despesaGateway;

    public CompraPlanejadaService(
        ICompraPlanejadaRepository repository,
        IUsuarioLogado usuarioLogado,
        ICompraPlanejadaDespesaGateway despesaGateway = null)
    {
        _repository = repository;
        _usuarioLogado = usuarioLogado;
        _despesaGateway = despesaGateway;
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Adicionar(CreateCompraPlanejadaDTO createDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        try
        {
            var links = CriarLinks(createDTO.LinksLojas);

            var compra = new Domain.Entity.CompraPlanejada(
                _usuarioLogado.IdContextoDados,
                createDTO.Nome,
                createDTO.ValorEstimado,
                createDTO.Prioridade,
                createDTO.Descricao,
                links);

            await _repository.Add(compra);

            return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
        }
        catch (DomainValidatorException exception)
        {
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
    }

    public async Task<Result<ListaComprasPlanejadasResponseDTO>> ListarPendentes()
    {
        var compras = await _repository.GetPendentes(_usuarioLogado.IdContextoDados);

        return Result.Success(new ListaComprasPlanejadasResponseDTO
        {
            Itens = compras.Select(CompraPlanejadaResponseDTO.Mapear).ToList(),
            TotalEstimado = compras.Sum(compra => compra.ValorEstimado)
        });
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Concluir(
        string id,
        ConcluirCompraPlanejadaDTO dto)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.NotFound("Compra planejada informada não existe."));

        var validacao = ValidarConclusao(dto);
        if (validacao is not null)
            return Result.Failure<CompraPlanejadaResponseDTO>(Error.Validation(validacao));

        string despesaId = null;
        var marcadaNestaOperacao = false;
        if (dto.CriarDespesa)
        {
            if (_despesaGateway is null)
                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.Exception("Integração com despesas não configurada.", new InvalidOperationException()));

            Result<string> resultadoDespesa;
            try
            {
                resultadoDespesa = await _despesaGateway.Criar(new CreateDespesaDTO
                {
                    Ano = dto.Ano,
                    Mes = dto.Mes,
                    Descricao = compra.Nome,
                    Valor = dto.ValorReal,
                    CategoriaId = dto.CategoriaId
                });
            }
            catch (Exception exception)
            {
                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.Exception("Não foi possível criar a despesa.", exception));
            }

            if (resultadoDespesa.IsFailure)
                return Result.Failure<CompraPlanejadaResponseDTO>(resultadoDespesa.Error);

            despesaId = resultadoDespesa.Value;
        }

        try
        {
            compra.MarcarComoComprado(dto.ValorReal, dto.DataCompra);
            marcadaNestaOperacao = true;

            if (!string.IsNullOrWhiteSpace(despesaId))
                compra.VincularDespesa(despesaId);

            if (!await _repository.AtualizarSeEstado(compra, EstadoCompraPlanejada.Pendente))
            {
                compra.ReverterCompra();
                var compensacao = await CompensarDespesa(despesaId);
                if (compensacao.IsFailure)
                    return Result.Failure<CompraPlanejadaResponseDTO>(compensacao.Error);

                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.NotFound("Compra planejada informada não existe mais."));
            }
        }
        catch (DomainValidatorException exception)
        {
            if (marcadaNestaOperacao && compra.Estado == EstadoCompraPlanejada.Comprado)
                compra.ReverterCompra();

            var compensacao = await CompensarDespesa(despesaId);
            if (compensacao.IsFailure)
                return Result.Failure<CompraPlanejadaResponseDTO>(compensacao.Error);

            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
        catch (Exception exception)
        {
            if (marcadaNestaOperacao && compra.Estado == EstadoCompraPlanejada.Comprado)
                compra.ReverterCompra();

            var compensacao = await CompensarDespesa(despesaId);
            if (compensacao.IsFailure)
                return Result.Failure<CompraPlanejadaResponseDTO>(compensacao.Error);

            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Exception("Não foi possível concluir a compra.", exception));
        }

        return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
    }

    public async Task<Result<ListaComprasCompradasResponseDTO>> ListarComprados()
    {
        var compras = await _repository.GetComprados(_usuarioLogado.IdContextoDados);

        return Result.Success(new ListaComprasCompradasResponseDTO
        {
            Itens = compras.Select(CompraPlanejadaResponseDTO.Mapear).ToList(),
            TotalEstimado = compras.Sum(compra => compra.ValorEstimado),
            TotalReal = compras.Sum(compra => compra.ValorReal ?? 0m)
        });
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Reverter(
        string id,
        ReverterCompraPlanejadaDTO dto)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.NotFound("Compra planejada informada não existe."));

        if (compra.Estado != EstadoCompraPlanejada.Comprado)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation("Somente itens comprados podem ser revertidos."));

        var valorRealOriginal = compra.ValorReal!.Value;
        var dataCompraOriginal = compra.DataCompra!.Value;
        var despesaIdOriginal = compra.DespesaId;
        var excluirDespesa = dto?.ExcluirDespesa == true && !string.IsNullOrWhiteSpace(despesaIdOriginal);

        try
        {
            compra.ReverterCompra();

            if (!await _repository.AtualizarSeEstado(compra, EstadoCompraPlanejada.Comprado))
            {
                RestaurarCompra(compra, valorRealOriginal, dataCompraOriginal, despesaIdOriginal);
                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.NotFound("Compra planejada informada não existe mais."));
            }

            if (excluirDespesa)
            {
                if (_despesaGateway is null)
                    throw new InvalidOperationException("Integração com despesas não configurada.");

                var exclusao = await _despesaGateway.Excluir(despesaIdOriginal);
                if (exclusao.IsFailure)
                {
                    await RestaurarCompraPersistida(
                        compra,
                        valorRealOriginal,
                        dataCompraOriginal,
                        despesaIdOriginal);
                    return Result.Failure<CompraPlanejadaResponseDTO>(exclusao.Error);
                }
            }
        }
        catch (DomainValidatorException exception)
        {
            RestaurarCompra(compra, valorRealOriginal, dataCompraOriginal, despesaIdOriginal);
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
        catch (Exception exception)
        {
            try
            {
                await RestaurarCompraPersistida(
                    compra,
                    valorRealOriginal,
                    dataCompraOriginal,
                    despesaIdOriginal);
            }
            catch (Exception rollbackException)
            {
                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.Exception("Falha ao reverter a compra e ao restaurar seu estado anterior.", rollbackException));
            }

            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Exception("Não foi possível reverter a compra.", exception));
        }

        return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
    }

    public async Task<Result<CompraPlanejadaResponseDTO>> Atualizar(UpdateCompraPlanejadaDTO updateDTO)
    {
        if (!PodeEditar())
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(updateDTO.Id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.NotFound("Compra planejada informada não existe."));

        if (compra.Estado != EstadoCompraPlanejada.Pendente)
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation("Somente itens pendentes podem ser alterados."));

        try
        {
            var links = CriarLinks(updateDTO.LinksLojas);

            compra.Atualizar(
                updateDTO.Nome,
                updateDTO.ValorEstimado,
                updateDTO.Prioridade,
                updateDTO.Descricao,
                links);

            if (!await _repository.AtualizarSePendente(compra))
                return Result.Failure<CompraPlanejadaResponseDTO>(
                    Error.NotFound("Compra planejada informada não existe mais."));

            return Result.Success(CompraPlanejadaResponseDTO.Mapear(compra));
        }
        catch (DomainValidatorException exception)
        {
            return Result.Failure<CompraPlanejadaResponseDTO>(
                Error.Validation(string.Join(" ", exception.Errors)));
        }
    }

    public async Task<Result> Excluir(string id)
    {
        if (!PodeEditar())
            return Result.Failure(
                Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var compra = await _repository.GetById(id, _usuarioLogado.IdContextoDados);
        if (compra is null)
            return Result.Failure(Error.NotFound("Compra planejada informada não existe."));

        await _repository.Delete(compra);

        return Result.Success();
    }

    private bool PodeEditar()
    {
        return !_usuarioLogado.EmModoCompartilhado
            || _usuarioLogado.PermissaoAtual == NivelPermissao.Editar;
    }

    private static List<LinkLojaCompraPlanejada> CriarLinks(
        IEnumerable<CompraPlanejadaLinkDTO> links)
    {
        return links?.Select(link =>
        {
            if (link is null)
                throw new DomainValidatorException("A lista de links da loja contém item inválido.");

            return new LinkLojaCompraPlanejada(link.Url, link.NomeLoja);
            }).ToList() ?? [];
    }

    private static string ValidarConclusao(ConcluirCompraPlanejadaDTO dto)
    {
        if (dto is null)
            return "Dados da conclusão são obrigatórios.";

        if (dto.ValorReal <= 0)
            return "Valor real deve ser maior que zero.";

        if (dto.DataCompra.Date > DateTime.UtcNow.Date)
            return "Data da compra não pode ser futura.";

        if (!dto.CriarDespesa)
            return null;

        if (dto.Ano <= 0)
            return "Ano da despesa é obrigatório.";

        if (dto.Mes < 1 || dto.Mes > 12)
            return "Mês da despesa deve estar entre 1 e 12.";

        if (string.IsNullOrWhiteSpace(dto.CategoriaId))
            return "Categoria da despesa é obrigatória.";

        return null;
    }

    private async Task<Result> CompensarDespesa(string despesaId)
    {
        if (string.IsNullOrWhiteSpace(despesaId) || _despesaGateway is null)
            return Result.Success();

        try
        {
            var resultado = await _despesaGateway.Excluir(despesaId);
            return resultado.IsFailure
                ? Result.Failure(resultado.Error)
                : Result.Success();
        }
        catch (Exception exception)
        {
            return Result.Failure(
                Error.Exception($"A despesa {despesaId} não pôde ser compensada.", exception));
        }
    }

    private static void RestaurarCompra(
        Domain.Entity.CompraPlanejada compra,
        decimal valorReal,
        DateTime dataCompra,
        string despesaId)
    {
        if (compra.Estado == EstadoCompraPlanejada.Pendente)
            compra.MarcarComoComprado(valorReal, dataCompra);

        compra.RemoverVinculoDespesa();
        if (!string.IsNullOrWhiteSpace(despesaId))
            compra.VincularDespesa(despesaId);
    }

    private async Task RestaurarCompraPersistida(
        Domain.Entity.CompraPlanejada compra,
        decimal valorReal,
        DateTime dataCompra,
        string despesaId)
    {
        RestaurarCompra(compra, valorReal, dataCompra, despesaId);

        if (!await _repository.AtualizarSeEstado(compra, EstadoCompraPlanejada.Pendente))
            throw new InvalidOperationException("O estado anterior da compra não pôde ser restaurado.");
    }
}
