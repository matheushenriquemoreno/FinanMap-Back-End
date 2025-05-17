using SharedDomain.Entity;

namespace Domain.Entity
{
    public class Despesa : Transacao, IClone<Despesa>
    {
        public string IdDespesaAgrupadora {  get; set; }
        public Despesa Agrupadora { get; set; }

        public bool? DespesaAgrupadora { get; protected set; }
        public int? QuantidadeRegistros { get; protected set; } = 0;

        public Despesa(int ano, int mes, string descricao, decimal valor, Categoria categoria, Usuario usuario)
            : base(ano, mes, descricao, valor, categoria, usuario)
        {
        }

        public void AdicionarDespesaAgrupadora(Despesa despesaAgrupadora)
        {
            IdDespesaAgrupadora = despesaAgrupadora.Id;
        }

        public void RemoverDespesaAgrupadora()
        {
            IdDespesaAgrupadora = null;
        }

        public void MarcarDespesaComoAgrupadora()
        {
            DespesaAgrupadora = true;
            QuantidadeRegistros++;
        }

        private void DesmarcarDespesaComoAgrupadora()
        {
            DespesaAgrupadora = false;
            QuantidadeRegistros = 0;
        }

        public void DiminuirAgrupamento(Despesa despesaDesvinculada)
        {
            QuantidadeRegistros--;

            if (QuantidadeRegistros <= 0)
            {
                DesmarcarDespesaComoAgrupadora();
            }

            this.Valor = this.Valor - despesaDesvinculada.Valor;
        }

        public bool EstaAgrupada()
        {
            return !string.IsNullOrEmpty(IdDespesaAgrupadora);
        }

        public bool EhAgrupadora()
        {
            return DespesaAgrupadora.HasValue && DespesaAgrupadora.Value;
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
