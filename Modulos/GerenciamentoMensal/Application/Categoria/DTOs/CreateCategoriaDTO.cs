using System.ComponentModel.DataAnnotations;
using Domain.Enum;

namespace Application.DTOs;

public class CreateCategoriaDTO
{
    public string Nome { get; set; }

    [Required(ErrorMessage = "Campo Tipo e obrigatorio!")]
    public TipoCategoria? Tipo { get; set; }
}

