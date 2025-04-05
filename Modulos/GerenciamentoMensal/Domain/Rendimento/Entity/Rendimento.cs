using SharedDomain.Entity;

namespace Domain.Entity
{
    public class Rendimento : Transacao, IClone<Rendimento>
    {
        public Rendimento(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
            : base(ano, mes, descricao, valor, categoria, usuario)
        {
        }

        public Rendimento Clone()
        {
            Rendimento clone = (Rendimento)this.MemberwiseClone();
            clone.Id = string.Empty;
            return clone;
        }

        protected override bool CategoriaEhValida(Categoria categoria)
        {
            if (categoria.Tipo == Enum.TipoCategoria.Rendimento)
                return true;

            return false;
        }
    }
}
