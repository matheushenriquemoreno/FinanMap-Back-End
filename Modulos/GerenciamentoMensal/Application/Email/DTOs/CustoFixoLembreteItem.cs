namespace Application.Email.DTOs;

public class CustoFixoLembreteItem
{
    public string Nome { get; set; }
    public int DiasRestantes { get; set; }

    public CustoFixoLembreteItem(string nome, int diasRestantes)
    {
        Nome = nome;
        DiasRestantes = diasRestantes;
    }
}
