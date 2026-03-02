using Application.DTOs;
using Application.Interfaces;
using Application.Shared.Transacao.DTOs;
using Domain.Compartilhamento.Entity;
using Domain.Entity;
using Domain.Enums;
using Domain.Login.Interfaces;
using Domain.Relatorios.AcumuladoMensal;
using Domain.Relatorios.Entity;
using Domain.Repository;

namespace Application.Services;

public class DespesaService : IDespesaService
{
    private readonly IDespesaRepository _repository;
    private readonly ICategoriaRepository _categoriaRepository;
    private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public DespesaService(IDespesaRepository repository, ICategoriaRepository categoriaRepository, IAcumuladoMensalReportRepository acumuladoMensalReportRepository, IUsuarioLogado usuarioLogado)
    {
        _repository = repository;
        _categoriaRepository = categoriaRepository;
        _acumuladoMensalReportRepository = acumuladoMensalReportRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<Result<ResultDespesaDTO>> Adicionar(CreateDespesaDTO createDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Categoria? categoria = await _categoriaRepository.GetById(createDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        Despesa despesa = new Despesa(createDTO.Ano, createDTO.Mes, createDTO.Descricao, createDTO.Valor, categoria, _usuarioLogado.UsuarioContextoDados);

        if (!string.IsNullOrEmpty(createDTO.IdDespesaAgrupadora))
        {
            var result = await VincularDespesaAUmaAgrupadora(createDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure<ResultDespesaDTO>(result.Error);
        }

        await _repository.Add(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    public async Task<Result<ResultDespesaDTO>> Atualizar(UpdateDespesaDTO updateDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesa = await _repository.GetById(updateDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        Categoria categoria = await _categoriaRepository.GetById(updateDTO.CategoriaId);

        if (categoria == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Categoria informada não existe!"));

        if (despesa.EhAgrupadora())
        {
            if (updateDTO.IdDespesaAgrupadora.PossuiValor())
                return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa que e usada para agrupar outras, não pode ser vinculada a nenhuma outra."));

            var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        despesa.Descricao = updateDTO.Descricao;
        despesa.AtualizarValor(updateDTO.Valor);
        despesa.PreencherCategoria(categoria);

        await AtualizarAgrupamento(despesa, updateDTO);

        await _repository.Update(despesa);

        await AtualizarDespesaAgrupadoraCasoNecessario(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        ResultDespesaDTO rendimentoDTO = ObterDespesaDTO(despesa, reportAcumulado);

        return Result.Success(rendimentoDTO);
    }

    private async Task AtualizarDespesaAgrupadoraCasoNecessario(Despesa despesa)
    {
        if (despesa.EstaAgrupada())
        {
            var despesaAgrupadora = await _repository.GetById(despesa.IdDespesaAgrupadora);

            var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesa.IdDespesaAgrupadora);

            if (despesaAgrupadora.Valor < valorAgrupamento)
            {
                despesaAgrupadora.Valor = valorAgrupamento;

                await _repository.Update(despesaAgrupadora);
            }

            despesa.Agrupadora = despesaAgrupadora;
        }
    }

    private async Task<Result> AtualizarAgrupamento(Despesa despesa, UpdateDespesaDTO updateDTO)
    {
        // Cenario de remover vinculo com a despesa agrupadora
        if (despesa.EstaAgrupada() &&
            updateDTO.IdDespesaAgrupadora.PossuiValor() == false)
        {
            await DesvincularDespesaDaAgrupadora(despesa, despesa.IdDespesaAgrupadora);
            return Result.Success();
        }

        // cenario de agrupamento após a criação da despesa
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.EstaAgrupada() == false)
        {
            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            return Result.Success();
        }

        // cenario de troca de agrupamento, era de uma despesa foi para outra.
        if (updateDTO.IdDespesaAgrupadora.PossuiValor() &&
                despesa.IdDespesaAgrupadora != updateDTO.IdDespesaAgrupadora)
        {
            await DesvincularDespesaDaAgrupadora(despesa, despesa.IdDespesaAgrupadora);

            var result = await VincularDespesaAUmaAgrupadora(updateDTO.IdDespesaAgrupadora, despesa);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            return result;
        }

        return Result.Success();
    }

    public async Task<Result> Excluir(string id)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        Despesa despesa = await _repository.GetById(id);

        if (despesa == null)
            return Result.Failure(Error.NotFound("Despesa informada não existente"));

        if (despesa.EstaAgrupada())
        {
            var despesaAgrupadora = await _repository.GetById(despesa.IdDespesaAgrupadora);
            await RemoverVinculoAgrupadoraEhAtualizar(despesaAgrupadora, despesa);
        }
        else if (despesa.EhAgrupadora())
        {
            var despesasFilhas = await _repository.GetDespesasDaAgrupadora(despesa.Id);

            foreach (var filha in despesasFilhas)
            {
                await _repository.Delete(filha);
            }
        }

        await _repository.Delete(despesa);

        return Result.Success();
    }

    public async Task<Result<ResultDespesaDTO>> ObterPeloID(string id)
    {
        var despesa = await _repository.GetById(id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informada não existente"));

        return Result.Success(ObterDespesaDTO(despesa));
    }

    public async Task<List<ResultDespesaDTO>> ObterMesAno(int mes, int ano, string descricao)
    {
        var despesas = await _repository.GetPeloMes(mes, ano, _usuarioLogado.IdContextoDados, descricao);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<List<ResultDespesaDTO>> ObterDespesasDaAgrupadora(string idDespesa)
    {
        var despesas = await _repository.GetDespesasDaAgrupadora(idDespesa);

        return despesas.Select(x => ObterDespesaDTO(x)).ToList();
    }

    public async Task<Result<ResultDespesaDTO>> AtualizarValor(UpdateValorTransacaoDTO updateValorTransacaoDTO)
    {
        // Verificar permissão em modo compartilhado
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure<ResultDespesaDTO>(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesa = await _repository.GetById(updateValorTransacaoDTO.Id);

        if (despesa == null)
            return Result.Failure<ResultDespesaDTO>(Error.NotFound("Despesa informado não existe!"));

        if (despesa.EhAgrupadora())
        {
            var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesa.Id);

            if (valorAgrupamento > updateValorTransacaoDTO.Valor)
                return Result.Failure<ResultDespesaDTO>(Error.Validation($"Valor informado menor que o total das despesas agrupadas, valor total: {valorAgrupamento}"));
        }

        despesa.AtualizarValor(updateValorTransacaoDTO.Valor);

        await _repository.Update(despesa);

        await AtualizarDespesaAgrupadoraCasoNecessario(despesa);

        var reportAcumulado = await _acumuladoMensalReportRepository.Obter(despesa.Mes, despesa.Ano, _usuarioLogado.IdContextoDados);

        return Result.Success(ObterDespesaDTO(despesa, reportAcumulado));
    }

    private static ResultDespesaDTO ObterDespesaDTO(Despesa despesa, AcumuladoMensalReport? reportAcumulado = null)
    {
        var result = new ResultDespesaDTO()
        {
            Ano = despesa.Ano,
            Mes = despesa.Mes,
            CategoriaNome = despesa.Categoria?.Nome,
            CategoriaId = despesa.CategoriaId,
            Id = despesa.Id,
            Descricao = despesa.Descricao,
            Valor = despesa.Valor,
            ReportAcumulado = reportAcumulado,
            EhDespesaAgrupadora = despesa.DespesaAgrupadora,
            IdDespesaAgrupadora = despesa.IdDespesaAgrupadora,
            Agrupadora = despesa.Agrupadora is not null ? ObterDespesaDTO(despesa.Agrupadora) : null,
            DespesaOrigemId = despesa.DespesaOrigemId,
            IsRecorrente = despesa.IsRecorrente,
            IsParcelado = despesa.IsParcelado,
            ParcelaAtual = despesa.ParcelaAtual,
            TotalParcelas = despesa.TotalParcelas
        };

        return result;
    }

    private async Task<Result> VincularDespesaAUmaAgrupadora(string idDespesaAgrupadora, Despesa despesa)
    {
        var despesaAgrupadora = await _repository.GetById(idDespesaAgrupadora);

        if (despesaAgrupadora == null)
            return Result.Failure(Error.NotFound("Despesa para realizar agrupamento não existe!"));

        despesa.AdicionarDespesaAgrupadora(despesaAgrupadora);
        despesaAgrupadora.MarcarDespesaComoAgrupadora();

        var valorAgrupamento = await _repository.GetValorTotalDespesasDaAgrupadora(despesaAgrupadora.Id);

        valorAgrupamento += despesa.Valor;

        if (valorAgrupamento > despesaAgrupadora.Valor)
        {
            var diferenca = valorAgrupamento - despesaAgrupadora.Valor;

            despesaAgrupadora.AtualizarValor(despesaAgrupadora.Valor + diferenca);
        }

        await _repository.Update(despesaAgrupadora);

        return Result.Success();
    }

    private async Task DesvincularDespesaDaAgrupadora(Despesa despesa, string idDespesaAgrupadora)
    {
        despesa.RemoverDespesaAgrupadora();

        var despesaAgrupadora = await _repository.GetById(idDespesaAgrupadora);
        await RemoverVinculoAgrupadoraEhAtualizar(despesaAgrupadora, despesa);
    }

    private async Task RemoverVinculoAgrupadoraEhAtualizar(Despesa despesaAgrupadora, Despesa despesa)
    {
        despesaAgrupadora.DiminuirAgrupamento(despesa);
        await _repository.Update(despesaAgrupadora);
    }

    public async Task<Result> LancarDespesaEmLoteAsync(LancarDespesaLoteDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        if (dto.QuantidadeMeses > 24)
            return Result.Failure(Error.Validation("A quantidade de meses para recorrência ou parcelamento não pode ser superior a 24 meses."));

        Categoria categoria = await _categoriaRepository.GetById(dto.CategoriaId);

        if (categoria == null)
            return Result.Failure(Error.NotFound("Categoria informada não existe!"));

        var despesas = new List<Domain.Entity.Despesa>();
        string despesaOrigemId = Guid.NewGuid().ToString();

        // Se tem agrupadora, buscar a agrupadora do mês inicial como referência
        Despesa agrupadoaReferencia = null;
        if (!string.IsNullOrEmpty(dto.IdDespesaAgrupadora))
        {
            agrupadoaReferencia = await _repository.GetById(dto.IdDespesaAgrupadora);
            if (agrupadoaReferencia == null)
                return Result.Failure(Error.NotFound("Despesa agrupadora não encontrada."));
        }

        int anoCorrente = dto.AnoInicial;
        int mesCorrente = dto.MesInicial;

        decimal valorParcelaNormal = dto.IsParcelado ? Math.Round(dto.ValorTotal / dto.QuantidadeMeses, 2) : dto.ValorTotal;
        decimal valorAcumulado = 0;

        for (int i = 1; i <= dto.QuantidadeMeses; i++)
        {
            decimal valor = valorParcelaNormal;

            if (dto.IsParcelado)
            {
                if (i == dto.QuantidadeMeses)
                {
                    valor = dto.ValorTotal - valorAcumulado;
                }
                valorAcumulado += valor;
            }

            string descricao = dto.Descricao;

            var despesa = new Despesa(anoCorrente, mesCorrente, descricao, valor, categoria, _usuarioLogado.UsuarioContextoDados)
            {
                DespesaOrigemId = despesaOrigemId,
                IsParcelado = dto.IsParcelado,
                IsRecorrente = dto.IsRecorrenteFixa,
                ParcelaAtual = dto.IsParcelado ? i : null,
                TotalParcelas = dto.IsParcelado ? dto.QuantidadeMeses : null
            };

            // NOVO: Vincular à agrupadora do mês correspondente
            if (agrupadoaReferencia != null)
            {
                var agrupadoaDoMes = await ObterOuClonarAgrupadora(agrupadoaReferencia, anoCorrente, mesCorrente);

                despesa.AdicionarDespesaAgrupadora(agrupadoaDoMes);
                agrupadoaDoMes.MarcarDespesaComoAgrupadora();

                // Atualizar o valor da agrupadora somando a nova filha
                agrupadoaDoMes.AtualizarValor(agrupadoaDoMes.Valor + valor);
                await _repository.Update(agrupadoaDoMes);
            }

            despesas.Add(despesa);

            mesCorrente++;
            if (mesCorrente > 12)
            {
                mesCorrente = 1;
                anoCorrente++;
            }
        }

        if (despesas.Any())
            await _repository.InsertManyAsync(despesas);

        return Result.Success();
    }

    public async Task<Result> AtualizarDespesaEmLoteAsync(string id, AtualizarLoteDespesaDTO dto)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesaAlvo = await _repository.GetById(id);
        if (despesaAlvo == null)
            return Result.Failure(Error.NotFound("Despesa alvo não encontrada."));

        if (string.IsNullOrEmpty(despesaAlvo.DespesaOrigemId))
            return Result.Failure(Error.Validation("Esta despesa não faz parte de um lote."));

        Categoria categoria = await _categoriaRepository.GetById(dto.NovaCategoriaId);
        if (categoria == null)
            return Result.Failure(Error.NotFound("Categoria não encontrada."));

        IEnumerable<Domain.Entity.Despesa> loteCompleto = await _repository.GetDespesasDoLoteAsync(despesaAlvo.DespesaOrigemId);
        IEnumerable<Domain.Entity.Despesa> despesasParaAtualizar = loteCompleto;

        if (dto.Modificador == ModificadorLote.ApenasEsta)
        {
            despesasParaAtualizar = loteCompleto.Where(d => d.Id == despesaAlvo.Id);
        }
        else if (dto.Modificador == ModificadorLote.EstaEProximas)
        {
            if (despesaAlvo.IsParcelado)
                despesasParaAtualizar = loteCompleto.Where(d => d.ParcelaAtual >= despesaAlvo.ParcelaAtual);
            else
                despesasParaAtualizar = loteCompleto.Where(d => d.Ano > despesaAlvo.Ano || (d.Ano == despesaAlvo.Ano && d.Mes >= despesaAlvo.Mes));
        }

        foreach (var despesa in despesasParaAtualizar)
        {
            if (despesa.IsParcelado)
            {
                despesa.Descricao = $"{dto.NovaDescricao} ({despesa.ParcelaAtual}/{despesa.TotalParcelas})";
                despesa.AtualizarValor(dto.NovoValor);
            }
            else
            {
                despesa.Descricao = dto.NovaDescricao;
                despesa.AtualizarValor(dto.NovoValor);
            }

            despesa.PreencherCategoria(categoria);
        }

        await _repository.UpdateManyAsync(despesasParaAtualizar);

        // Recalcular o valor de cada agrupadora afetada
        var agrupadorasAfetadas = new HashSet<string>();
        foreach (var despesa in despesasParaAtualizar)
        {
            if (despesa.EstaAgrupada())
                agrupadorasAfetadas.Add(despesa.IdDespesaAgrupadora);
        }

        foreach (var idAgrupadora in agrupadorasAfetadas)
        {
            var agrupadora = await _repository.GetById(idAgrupadora);
            if (agrupadora != null)
            {
                var valorTotal = await _repository.GetValorTotalDespesasDaAgrupadora(idAgrupadora);
                if (agrupadora.Valor < valorTotal)
                {
                    agrupadora.AtualizarValor(valorTotal);
                    await _repository.Update(agrupadora);
                }
            }
        }

        return Result.Success();
    }

    public async Task<Result> ExcluirDespesaEmLoteAsync(string id, ModificadorLote modificador)
    {
        if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
            return Result.Failure(Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

        var despesaAlvo = await _repository.GetById(id);
        if (despesaAlvo == null)
            return Result.Failure(Error.NotFound("Despesa alvo não encontrada."));

        if (string.IsNullOrEmpty(despesaAlvo.DespesaOrigemId))
            return Result.Failure(Error.Validation("Esta despesa não faz parte de um lote."));

        IEnumerable<Domain.Entity.Despesa> loteCompleto = await _repository.GetDespesasDoLoteAsync(despesaAlvo.DespesaOrigemId);
        IEnumerable<Domain.Entity.Despesa> despesasParaExcluir = loteCompleto;

        if (modificador == ModificadorLote.ApenasEsta)
        {
            despesasParaExcluir = loteCompleto.Where(d => d.Id == despesaAlvo.Id);
        }
        else if (modificador == ModificadorLote.EstaEProximas)
        {
            if (despesaAlvo.IsParcelado)
                despesasParaExcluir = loteCompleto.Where(d => d.ParcelaAtual >= despesaAlvo.ParcelaAtual);
            else
                despesasParaExcluir = loteCompleto.Where(d => d.Ano > despesaAlvo.Ano || (d.Ano == despesaAlvo.Ano && d.Mes >= despesaAlvo.Mes));
        }

        // Coletar as agrupadoras que precisam ser atualizadas antes da exclusão
        var agrupadorasParaAtualizar = new Dictionary<string, decimal>();
        foreach (var despesa in despesasParaExcluir)
        {
            if (despesa.EstaAgrupada())
            {
                if (!agrupadorasParaAtualizar.ContainsKey(despesa.IdDespesaAgrupadora))
                    agrupadorasParaAtualizar[despesa.IdDespesaAgrupadora] = 0;

                agrupadorasParaAtualizar[despesa.IdDespesaAgrupadora] += despesa.Valor;
            }
        }

        await _repository.DeleteManyAsync(despesasParaExcluir);

        // Atualizar as agrupadoras
        foreach (var agrupadoraAtualizar in agrupadorasParaAtualizar)
        {
            var agrupadora = await _repository.GetById(agrupadoraAtualizar.Key);
            if (agrupadora != null)
            {
                // Como não temos a entidade despesa em si (já foi excluída), 
                // para cada ocorrência deduzimos o agrupamento
                foreach (var despesaExcluida in despesasParaExcluir.Where(x => x.IdDespesaAgrupadora == agrupadoraAtualizar.Key))
                {
                    agrupadora.DiminuirAgrupamento(despesaExcluida);
                }
                await _repository.Update(agrupadora);
            }
        }

        return Result.Success();
    }

    private async Task<Despesa> ObterOuClonarAgrupadora(Despesa agrupadoaReferencia, int ano, int mes)
    {
        // Caso 1: A agrupadora é do próprio mês que estamos criando
        if (agrupadoaReferencia.Ano == ano && agrupadoaReferencia.Mes == mes)
            return agrupadoaReferencia;

        // Caso 2: Buscar agrupadora existente no mês/ano alvo
        var agrupadorasNoMes = await _repository.GetWhere(
            x => x.Ano == ano
                 && x.Mes == mes
                 && x.Descricao == agrupadoaReferencia.Descricao
                 && x.CategoriaId == agrupadoaReferencia.CategoriaId
                 && x.UsuarioId == agrupadoaReferencia.UsuarioId);

        var agrupadoaExistente = agrupadorasNoMes.FirstOrDefault(x => x.EhAgrupadora()
            || x.Descricao == agrupadoaReferencia.Descricao);

        if (agrupadoaExistente != null)
            return agrupadoaExistente;

        // Caso 3: Clonar a agrupadora para o mês futuro
        var clone = agrupadoaReferencia.Clone();
        clone.Ano = ano;
        clone.Mes = mes;
        clone.AtualizarValor(0); // Inicia com valor zero, será incrementado pelas filhas
        clone.MarcarDespesaComoAgrupadora();

        // Limpar dados de lote da agrupadora clonada (agrupadora não faz parte de lote)
        clone.DespesaOrigemId = null;
        clone.IsParcelado = false;
        clone.IsRecorrente = false;
        clone.ParcelaAtual = null;
        clone.TotalParcelas = null;

        await _repository.Add(clone);
        return clone;
    }
}
