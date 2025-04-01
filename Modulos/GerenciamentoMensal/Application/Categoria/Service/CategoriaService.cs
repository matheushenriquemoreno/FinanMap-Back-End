using Application.DTOs;
using Application.Interfaces;
using Domain.Entity;
using Domain.Enum;
using Domain.Login.Interfaces;
using Domain.Repository;

namespace Application.Implementacoes
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IUsuarioLogado _usuarioLogado;

        public CategoriaService(ICategoriaRepository categoriaRepository, IUsuarioLogado usuarioLogado)
        {
            _categoriaRepository = categoriaRepository;
            _usuarioLogado = usuarioLogado;
        }

        public async Task<Result<ResultCategoriaDTO>> Adicionar(CreateCategoriaDTO categoriaDTO)
        {
            if (_categoriaRepository.CategoriaJaExiste(categoriaDTO.Nome, _usuarioLogado.Id, categoriaDTO.Tipo.Value))
                return Result.Failure<ResultCategoriaDTO>(Error.Validation("Não e possivel criar categorias duplicadas!"));

            var categoria = new Categoria(categoriaDTO.Nome, categoriaDTO.Tipo.Value, _usuarioLogado.Id);

            categoria = await _categoriaRepository.Add(categoria);

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }

        public async Task<Result<ResultCategoriaDTO>> Atualizar(UpdateCategoriaDTO categoriaDTO)
        {
            var categoria = await _categoriaRepository.GetByID(categoriaDTO.Id);

            if (categoria is null)
                return Result.Failure<ResultCategoriaDTO>(Error.NotFound("Categoria informada não existe!"));

            categoria.AtualizarNome(categoriaDTO.Nome);

            await _categoriaRepository.Update(categoria);

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }

        public async Task<Result> Excluir(string id)
        {
            var categoria = await _categoriaRepository.GetByID(id);

            if (categoria is null)
                return Result.Failure(Error.NotFound("Categoria informada não existe!"));

            if (await _categoriaRepository.CategoriaJaFoiVinculado(categoria))
                return Result.Failure(Error.Validation("Categoria informada possui vinculos realizados, por favor removas e após realize a exclusão!"));

            await _categoriaRepository.Delete(categoria);

            return Result.Success();
        }

        public async Task<Result<List<ResultCategoriaDTO>>> ObterCategoria(TipoCategoria tipoCategoria, string nome)
        {
            var categorias = await _categoriaRepository.GetCategorias(tipoCategoria, nome, _usuarioLogado.Id);

            return Result.Success(categorias.Select(ResultCategoriaDTO.Mapear).ToList());
        }

        public async Task<Result<ResultCategoriaDTO>> ObterPeloID(string id)
        {
            var categoria = await _categoriaRepository.GetByID(id);

            if (categoria is null)
                return Result.Failure<ResultCategoriaDTO>(Error.NotFound("Categoria informada não existe!"));

            return Result.Success(ResultCategoriaDTO.Mapear(categoria));
        }
    }
}
