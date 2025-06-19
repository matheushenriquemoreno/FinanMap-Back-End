namespace Application.IA
{
    public interface IAGenerativaChat
    {
        string GetReponse(string instrucao, params string[] mensagens);
    }
}
