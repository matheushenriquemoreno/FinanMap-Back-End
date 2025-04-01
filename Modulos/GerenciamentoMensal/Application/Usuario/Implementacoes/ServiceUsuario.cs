using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Interface;
using Domain.Login.Interfaces;

namespace Application.Implementacoes
{
    public class ServiceUsuario : IServiceUsuario
    {
        private readonly IUsuarioLogado UsuarioLogado;

        public ServiceUsuario(IUsuarioLogado usuarioLogado)
        {
            UsuarioLogado = usuarioLogado;
        }

        public UsuarioDTO ObterUsuarioLogado()
        {
            var usuario = UsuarioLogado.Usuario;

            return new UsuarioDTO()
            {
                Email = usuario.Email,
                Nome = usuario.Nome,
            };
        }
    }
}
