using Application.Compartilhamento.DTOs;
using Application.Compartilhamento.Interfaces;
using Domain.Compartilhamento.Entity;
using Domain.Compartilhamento.Repository;
using Domain.Login.Interfaces;
using Domain.Repository;
using Mapster;

namespace Application.Compartilhamento.Service;

public class CompartilhamentoService : ICompartilhamentoService
{
    private readonly ICompartilhamentoRepository _compartilhamentoRepository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IUsuarioRepository _usuarioRepository;

    public CompartilhamentoService(
        ICompartilhamentoRepository compartilhamentoRepository,
        IUsuarioLogado usuarioLogado,
        IUsuarioRepository usuarioRepository)
    {
        _compartilhamentoRepository = compartilhamentoRepository;
        _usuarioLogado = usuarioLogado;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<Result<ResultCompartilhamentoDTO>> Convidar(CriarCompartilhamentoDTO dto)
    {
        // 1. Validar que o e-mail do convidado é diferente do e-mail do usuário logado
        var usuarioLogado = _usuarioLogado.Usuario;
        if (usuarioLogado.Email.Equals(dto.ConvidadoEmail, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ResultCompartilhamentoDTO>(Error.Validation("Não é possível compartilhar com você mesmo!"));

        // 2. Buscar o usuário convidado pelo e-mail
        var convidado = await _usuarioRepository.GetByEmail(dto.ConvidadoEmail);
        if (convidado == null)
            return Result.Failure<ResultCompartilhamentoDTO>(Error.NotFound("Usuário com este e-mail não encontrado!"));

        // 3. Verificar se já existe um compartilhamento ativo entre proprietário e convidado
        var compartilhamentoExistente = await _compartilhamentoRepository
            .ObterPorProprietarioEConvidado(_usuarioLogado.Id, convidado.Id);

        if (compartilhamentoExistente != null)
            return Result.Failure<ResultCompartilhamentoDTO>(Error.Validation("Já existe um compartilhamento com este usuário!"));

        // 4. Criar a entidade Compartilhamento
        var compartilhamento = new Domain.Compartilhamento.Entity.Compartilhamento(
            proprietarioId: _usuarioLogado.Id,
            proprietarioEmail: usuarioLogado.Email,
            proprietarioNome: usuarioLogado.Nome,
            convidadoId: convidado.Id,
            convidadoEmail: convidado.Email,
            permissao: dto.Permissao
        );

        // 5. Salvar no repositório
        await _compartilhamentoRepository.Add(compartilhamento);

        // 6. Retornar o DTO mapeado
        return Result.Success(compartilhamento.Adapt<ResultCompartilhamentoDTO>());
    }

    public async Task<List<ResultCompartilhamentoDTO>> ObterMeusCompartilhamentos()
    {
        var compartilhamentos = await _compartilhamentoRepository.ObterPorProprietarioId(_usuarioLogado.Id);
        return compartilhamentos.Select(c => c.Adapt<ResultCompartilhamentoDTO>()).ToList();
    }

    public async Task<List<ResultCompartilhamentoDTO>> ObterConvitesRecebidos()
    {
        var compartilhamentos = await _compartilhamentoRepository.ObterPorConvidadoId(_usuarioLogado.Id);
        return compartilhamentos.Select(c => c.Adapt<ResultCompartilhamentoDTO>()).ToList();
    }

    public async Task<Result> ResponderConvite(ResponderConviteDTO dto)
    {
        // 1. Buscar o compartilhamento
        var compartilhamento = await _compartilhamentoRepository.GetById(dto.CompartilhamentoId);
        if (compartilhamento == null)
            return Result.Failure(Error.NotFound("Compartilhamento não encontrado!"));

        // 2. Validar que o ConvidadoId é o usuário logado
        if (compartilhamento.ConvidadoId != _usuarioLogado.Id)
            return Result.Failure(Error.Forbidden("Apenas o convidado pode responder ao convite!"));

        // 3. Validar que o Status atual é Pendente
        if (compartilhamento.Status != StatusConvite.Pendente)
            return Result.Failure(Error.Validation("Este convite já foi respondido!"));

        // 4. Atualizar Status
        if (dto.Aceitar)
            compartilhamento.Aceitar();
        else
            compartilhamento.Recusar();

        // 5. Salvar
        await _compartilhamentoRepository.Update(compartilhamento);

        return Result.Success();
    }

    public async Task<Result> AtualizarPermissao(AtualizarPermissaoDTO dto)
    {
        // 1. Buscar o compartilhamento
        var compartilhamento = await _compartilhamentoRepository.GetById(dto.CompartilhamentoId);
        if (compartilhamento == null)
            return Result.Failure(Error.NotFound("Compartilhamento não encontrado!"));

        // 2. Validar que o ProprietarioId é o usuário logado
        if (compartilhamento.ProprietarioId != _usuarioLogado.Id)
            return Result.Failure(Error.Forbidden("Apenas o proprietário pode alterar a permissão!"));

        // 3. Atualizar a Permissao
        compartilhamento.AtualizarPermissao(dto.NovaPermissao);

        // 4. Salvar
        await _compartilhamentoRepository.Update(compartilhamento);

        return Result.Success();
    }

    public async Task<Result> Revogar(string compartilhamentoId)
    {
        // 1. Buscar o compartilhamento
        var compartilhamento = await _compartilhamentoRepository.GetById(compartilhamentoId);
        if (compartilhamento == null)
            return Result.Failure(Error.NotFound("Compartilhamento não encontrado!"));

        // 2. Validar que o ProprietarioId é o usuário logado
        if (compartilhamento.ProprietarioId != _usuarioLogado.Id)
            return Result.Failure(Error.Forbidden("Apenas o proprietário pode revogar o compartilhamento!"));

        // 3. Deletar
        await _compartilhamentoRepository.Delete(compartilhamento);

        return Result.Success();
    }
}
