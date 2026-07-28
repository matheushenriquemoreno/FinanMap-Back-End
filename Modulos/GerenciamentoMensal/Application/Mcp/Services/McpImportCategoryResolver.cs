#nullable enable

using System.Globalization;
using System.Text;
using Application.Mcp.Models;
using Domain.Enum;
using Domain.Repository;

namespace Application.Mcp.Services;

public sealed class McpImportCategoryResolver(
    ICategoriaRepository categories) : IMcpImportCategoryResolver
{
    public Task<IReadOnlyList<McpImportCategoryMatch>> FindMatchesAsync(
        string userId,
        string hint,
        TipoCategoria expectedType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = Normalize(hint);
        var matches = categories.GetCategorias()
            .Where(category =>
                category.UsuarioId == userId &&
                category.Tipo == expectedType)
            .AsEnumerable()
            .Where(category => Normalize(category.Nome) == normalized)
            .Select(category => new McpImportCategoryMatch(
                category.Id,
                category.Nome,
                category.Tipo))
            .ToArray();
        return Task.FromResult<IReadOnlyList<McpImportCategoryMatch>>(matches);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
