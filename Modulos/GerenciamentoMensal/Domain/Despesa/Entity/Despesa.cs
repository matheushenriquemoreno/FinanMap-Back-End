namespace Domain.Entity
{
    public class Despesa : Transacao
    {
        public Despesa(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
            : base(ano, mes, descricao, valor, categoria, usuario)
        {
        }

        protected override bool CategoriaEhValida(Categoria categoria)
        {
            if (categoria.Tipo == Enum.TipoCategoria.Despesa)
                return true;

            return false;
        }
    }
}
