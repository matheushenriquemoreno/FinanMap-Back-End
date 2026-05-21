using System.ComponentModel.DataAnnotations;

namespace Application.CustoFixo.DTOs;

public class UpdateCustoFixoDTO
{
    public string Id { get; set; }

    [Required(ErrorMessage = "Nome do custo fixo e obrigatorio!")]
    public string Nome { get; set; }

    [Range(1, 31, ErrorMessage = "Dia de vencimento deve estar entre 1 e 31!")]
    public int DiaVencimento { get; set; }

    public string CategoriaId { get; set; }
    public bool Ativo { get; set; }
}
