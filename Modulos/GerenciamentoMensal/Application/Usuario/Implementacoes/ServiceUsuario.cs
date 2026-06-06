using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Interface;
using Domain.Exceptions;
using Domain.Login.Interfaces;
using Domain.Repository;

namespace Application.Implementacoes
{
    public class ServiceUsuario : IServiceUsuario
    {
        private readonly IUsuarioLogado UsuarioLogado;
        private readonly IUsuarioRepository _usuarioRepository;

        public ServiceUsuario(IUsuarioLogado usuarioLogado, IUsuarioRepository usuarioRepository)
        {
            UsuarioLogado = usuarioLogado;
            _usuarioRepository = usuarioRepository;
        }

        public UsuarioDTO ObterUsuarioLogado()
        {
            var usuario = UsuarioLogado.Usuario;

            return new UsuarioDTO()
            {
                Email = usuario.Email,
                Nome = usuario.Nome,
                AvatarId = usuario.AvatarId,
            };
        }

        public async Task<Result<AtualizarAvatarDTO>> AtualizarAvatarAsync(AtualizarAvatarDTO dto)
        {
            var usuario = await _usuarioRepository.GetById(UsuarioLogado.Id);

            if (usuario == null)
            {
                return Result.Failure<AtualizarAvatarDTO>(Error.NotFound("Usuário não encontrado no banco de dados."));
            }

            try
            {
                usuario.AtualizarAvatar(dto.AvatarId);
            }
            catch (DomainValidatorException ex)
            {
                return Result.Failure<AtualizarAvatarDTO>(Error.Validation(string.Join(" ", ex.Errors)));
            }

            await _usuarioRepository.Update(usuario);

            return Result.Success(new AtualizarAvatarDTO { AvatarId = usuario.AvatarId });
        }

        public async Task<CustoFixoConfiguracaoDTO> ObterConfiguracaoCustoFixoAsync()
        {
            var usuarioId = UsuarioLogado.Id;
            var usuario = await _usuarioRepository.GetById(usuarioId);

            if (usuario == null)
            {
                return new CustoFixoConfiguracaoDTO { ReceberNotificacoes = UsuarioLogado.Usuario.ReceberNotificacoesCustosFixos };
            }

            return new CustoFixoConfiguracaoDTO
            {
                ReceberNotificacoes = usuario.ReceberNotificacoesCustosFixos
            };
        }

        public async Task<Result> AtualizarConfiguracaoCustoFixoAsync(CustoFixoConfiguracaoDTO configuracao)
        {
            var usuarioId = UsuarioLogado.Id;
            var usuario = await _usuarioRepository.GetById(usuarioId);

            if (usuario == null)
            {
                return Result.Failure(Error.NotFound("Usuário não encontrado no banco de dados."));
            }

            usuario.ReceberNotificacoesCustosFixos = configuracao.ReceberNotificacoes;
            await _usuarioRepository.Update(usuario);

            return Result.Success();
        }
    }
}
