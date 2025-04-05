using SharedDomain.Entity;

namespace Domain.Entity;

public class Investimento : Transacao, IClone<Investimento>
{
    public Investimento(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
        : base(ano, mes, descricao, valor, categoria, usuario)
    {
    }

    public Investimento Clone()
    {
        Investimento clone = (Investimento)this.MemberwiseClone();
        clone.Id = string.Empty;
        return clone;
    }

    protected override bool CategoriaEhValida(Categoria categoria)
    {
        if (categoria.Tipo == Enum.TipoCategoria.Investimento)
            return true;

        return false;
    }
}
