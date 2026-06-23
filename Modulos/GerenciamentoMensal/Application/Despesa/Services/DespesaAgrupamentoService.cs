using Domain.Entity;
using Domain.Repository;

namespace Application.Services;

public sealed class DespesaAgrupamentoContexto
{
    public DespesaAgrupamentoContexto(Despesa agrupadora, decimal valorBase)
    {
        Agrupadora = agrupadora;
        ValorBase = valorBase;
    }

    public Despesa Agrupadora { get; }

    // Motivador: a agrupadora guarda no mesmo campo Valor tanto seu valor proprio
    // quanto a soma das filhas. Antes de alterar/remover/trocar filhas, precisamos
    // separar e congelar essa base; caso contrario, o recalculo posterior pode
    // sobrescrever uma agrupadora de 3000 + 1000 para apenas 1000, ou inflar a base.
    // Foto do valor proprio da agrupadora antes de alterar suas filhas.
    // Sem isso, uma remocao/troca de filha mudaria a soma das filhas e faria
    // o sistema recalcular uma base errada.
    public decimal ValorBase { get; }
}

public sealed class DespesaAgrupamentoService
{
    private readonly IDespesaRepository _repository;

    public DespesaAgrupamentoService(IDespesaRepository repository)
    {
        _repository = repository;
    }

    public async Task<DespesaAgrupamentoContexto> CapturarContextoAsync(string idDespesaAgrupadora)
    {
        if (string.IsNullOrEmpty(idDespesaAgrupadora))
            return null;

        var agrupadora = await _repository.GetById(idDespesaAgrupadora);
        if (agrupadora == null)
            return null;

        // O valor base precisa ser capturado antes de qualquer mutacao nas filhas.
        // Depois, a sincronizacao sempre aplica: ValorBase + soma atual das filhas.
        var somaFilhas = await _repository.GetValorTotalDespesasDaAgrupadora(idDespesaAgrupadora);
        return new DespesaAgrupamentoContexto(agrupadora, agrupadora.Valor - somaFilhas);
    }

    public async Task SincronizarAgrupadoraAsync(DespesaAgrupamentoContexto contexto)
    {
        if (contexto == null)
            return;

        var filhas = (await _repository.GetDespesasDaAgrupadora(contexto.Agrupadora.Id)).ToList();
        AtualizarTotalEEstado(contexto, filhas.Sum(x => x.Valor), filhas.Count);

        await _repository.Update(contexto.Agrupadora);
    }

    public async Task SincronizarAgrupadoraComFilhasPendentesAsync(
        DespesaAgrupamentoContexto contexto,
        IEnumerable<Despesa> filhasPendentes)
    {
        if (contexto == null)
            return;

        var filhasPendentesDaAgrupadora = filhasPendentes
            .Where(x => x.IdDespesaAgrupadora == contexto.Agrupadora.Id)
            .ToList();

        var filhasPersistidas = (await _repository.GetDespesasDaAgrupadora(contexto.Agrupadora.Id)).ToList();
        var somaFilhas = filhasPersistidas.Sum(x => x.Valor) + filhasPendentesDaAgrupadora.Sum(x => x.Valor);
        var quantidadeFilhas = filhasPersistidas.Count + filhasPendentesDaAgrupadora.Count;

        AtualizarTotalEEstado(contexto, somaFilhas, quantidadeFilhas);
        await _repository.Update(contexto.Agrupadora);
    }

    public void Vincular(Despesa despesa, Despesa agrupadora)
    {
        despesa.AdicionarDespesaAgrupadora(agrupadora);
        despesa.Agrupadora = agrupadora;
    }

    public void RemoverVinculo(Despesa despesa)
    {
        despesa.RemoverDespesaAgrupadora();
        despesa.Agrupadora = null;
    }

    private static void AtualizarTotalEEstado(
        DespesaAgrupamentoContexto contexto,
        decimal somaFilhas,
        int quantidadeFilhas)
    {
        contexto.Agrupadora.AtualizarValor(contexto.ValorBase + somaFilhas);
        contexto.Agrupadora.DefinirQuantidadeRegistrosAgrupados(quantidadeFilhas);
    }
}
