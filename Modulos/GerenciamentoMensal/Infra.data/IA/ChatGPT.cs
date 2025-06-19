using Application.IA;
using Infra.Configure.VariaveisAmbiente;
using OpenAI.Chat;

namespace Infra.IA
{
    internal class ChatGPT : IAGenerativaChat
    {
        private static string GPT_API_KEY = OpenApi.Key;
        private static string GPT_Model = "gpt-4.1-mini";

        private static ChatClient client = new(
            model: GPT_Model,
            apiKey: GPT_API_KEY
            );

        ChatCompletionOptions options = new ChatCompletionOptions
        {
            Temperature = 0.6f,
            MaxOutputTokenCount = 1000,

        };

        public string GetReponse(string instrucao, params string[] mensagens)
        {
            var prompts = new List<ChatMessage>();

            AssistantChatMessage instrucaoPropt = new AssistantChatMessage(instrucao);

            prompts.Add(instrucaoPropt);

            foreach(var mensagem in mensagens)
            {
                AssistantChatMessage propt = new AssistantChatMessage(mensagem);
                prompts.Add(propt);
            }

            ChatCompletion completion = client.CompleteChat(prompts, options: options);

            var result = completion.Content[0].Text;
            return result;
        }
    }
}
