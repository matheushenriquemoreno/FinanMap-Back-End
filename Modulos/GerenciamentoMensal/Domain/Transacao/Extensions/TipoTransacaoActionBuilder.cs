namespace Domain;

public class TipoTransacaoActionBuilder
{
    private TipoTransacao Tipo;
    private Func<Task> _acaoRendimento;
    private Func<Task> _acaoDespesa;
    private Func<Task> _acaoInvestimento;

    public TipoTransacaoActionBuilder(TipoTransacao tipo)
    {
        Tipo = tipo;
    }

    public TipoTransacaoActionBuilder QuandoRendimento(Func<Task> acao)
    {
        _acaoRendimento = acao;
        return this;
    }

    public TipoTransacaoActionBuilder QuandoDespesa(Func<Task> acao)
    {
        _acaoDespesa = acao;
        return this;
    }

    public TipoTransacaoActionBuilder QuandoInvestimento(Func<Task> acao)
    {
        _acaoInvestimento = acao;
        return this;
    }

    public async Task ExecutarAsync()
    {
        switch (this.Tipo)
        {
            case TipoTransacao.Rendimento when _acaoRendimento is not null:
                await _acaoRendimento();
                break;
            case TipoTransacao.Despesa when _acaoDespesa is not null:
                await _acaoDespesa();
                break;
            case TipoTransacao.Investimento when _acaoInvestimento is not null:
                await _acaoInvestimento();
                break;
            default:
                throw new InvalidOperationException($"Nenhuma ação definida para o tipo '{this.Tipo}'.");
        }
    }
}

