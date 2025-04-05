using SharedDomain.Entity;

namespace Domain.Entity
{
    public class Despesa : Transacao, IClone<Despesa>
    {
        public Despesa(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
            : base(ano, mes, descricao, valor, categoria, usuario)
        {
        }

        public Despesa Clone()
        {
            Despesa clone = (Despesa)this.MemberwiseClone();
            clone.Id = string.Empty;
            return clone;
        }

        protected override bool CategoriaEhValida(Categoria categoria)
        {
            if (categoria.Tipo == Enum.TipoCategoria.Despesa)
                return true;

            return false;
        }
    }
}
