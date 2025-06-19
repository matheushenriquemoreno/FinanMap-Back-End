using Domain.Enum;

namespace Application.Interfaces
{
    public interface ISugestaoCategoria
    {
        Result<List<string>> ObterSurgestoesDeCategoriaBaseadoNoItemACadastrar(TipoCategoria tipo, string nomeItem);
    }
}
