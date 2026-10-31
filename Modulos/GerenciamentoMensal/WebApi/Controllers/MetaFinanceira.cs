using Application.MetaFinanceira.DTOs;
using Application.MetaFinanceira.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

public static class MetaFinanceira
{
    public static RouteGroupBuilder MapMetaFinanceiraEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/MetasFinanceiras");

        // GET /api/MetasFinanceiras — Listar todas as metas do usuário (?arquivadas=true|false; omitido = todas)
        group.MapGet("/", async (IMetaFinanceiraService service, string? arquivadas) =>
        {
            var filtroArquivadas = ParseArquivadas(arquivadas);
            var result = await service.ObterTodas(filtroArquivadas);
            return Results.Ok(result);
        });

        // GET /api/MetasFinanceiras/resumo — Painel de resumo (3 cards); segue o mesmo filtro ?arquivadas da listagem
        group.MapGet("/resumo", async (IMetaFinanceiraService service, string? arquivadas) =>
        {
            var filtroArquivadas = ParseArquivadas(arquivadas);
            var result = await service.ObterResumo(filtroArquivadas);
            return Results.Ok(result);
        });

        // GET /api/MetasFinanceiras/{id} — Obter meta por ID
        group.MapGet("/{id:length(24)}", async (string id, IMetaFinanceiraService service) =>
        {
            var result = await service.ObterPeloID(id);
            return result.MapResult();
        });

        // POST /api/MetasFinanceiras — Criar meta
        group.MapPost("/", async (CreateMetaFinanceiraDTO dto, IMetaFinanceiraService service) =>
        {
            var result = await service.Adicionar(dto);
            return result.MapResultCreated();
        });

        // PUT /api/MetasFinanceiras — Atualizar meta
        group.MapPut("/", async (UpdateMetaFinanceiraDTO dto, IMetaFinanceiraService service) =>
        {
            var result = await service.Atualizar(dto);
            return result.MapResult();
        });

        // DELETE /api/MetasFinanceiras/{id} — Excluir meta
        group.MapDelete("/{id:length(24)}", async (string id, IMetaFinanceiraService service) =>
        {
            var result = await service.Excluir(id);
            return result.MapResult();
        });

        // POST /api/MetasFinanceiras/{id}/contribuicoes — Adicionar contribuição
        group.MapPost("/{id:length(24)}/contribuicoes", async (
            string id,
            ContribuicaoDTO dto,
            IMetaFinanceiraService service) =>
        {
            var result = await service.AdicionarContribuicao(id, dto);
            return result.MapResultCreated();
        });

        // DELETE /api/MetasFinanceiras/{metaId}/contribuicoes/{contribId}
        group.MapDelete("/{metaId:length(24)}/contribuicoes/{contribId}", async (
            string metaId,
            string contribId,
            IMetaFinanceiraService service) =>
        {
            var result = await service.RemoverContribuicao(metaId, contribId);
            return result.MapResult();
        });

        // PUT /api/MetasFinanceiras/{id}/contribuicoes — Editar contribuição
        group.MapPut("/{id:length(24)}/contribuicoes", async (
            string id,
            UpdateContribuicaoDTO dto,
            IMetaFinanceiraService service) =>
        {
            var result = await service.EditarContribuicao(id, dto);
            return result.MapResult();
        });

        // POST /api/MetasFinanceiras/{id}/arquivar — Arquivar meta (em andamento ou concluída)
        group.MapPost("/{id:length(24)}/arquivar", async (
            string id,
            IMetaFinanceiraService service) =>
        {
            var result = await service.Arquivar(id);
            return result.MapResult();
        });

        // POST /api/MetasFinanceiras/{id}/desarquivar — Retirar meta do arquivo
        group.MapPost("/{id:length(24)}/desarquivar", async (
            string id,
            IMetaFinanceiraService service) =>
        {
            var result = await service.Desarquivar(id);
            return result.MapResult();
        });

        return group;
    }

    /// <summary>
    /// Interpreta o parâmetro de query ?arquivadas=true|false.
    /// Valor ausente ou inválido é tratado como null (retorna todas as metas).
    /// </summary>
    private static bool? ParseArquivadas(string? arquivadas)
    {
        return bool.TryParse(arquivadas, out var valor) ? valor : null;
    }
}
