using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SummarizerApi.Services;

public class OllamaLLMService : ILLMService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;

    public OllamaLLMService(IConfiguration config)
    {
        string chatModel = config["Ollama:ChatModel"];
        string embeddingModel = config["Ollama:EmbeddingModel"];
        Uri url = new(config["Ollama:Endpoint"]);

        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(chatModel, url);
        builder.AddOllamaEmbeddingGenerator(embeddingModel, url);
        _kernel = builder.Build();

        _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        _embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embeddings = await _embeddingService.GenerateAsync([text]);
        return embeddings.FirstOrDefault()?.Vector.ToArray() ?? [];
    }

    public async Task<string> GetLLMResponseAsync(string userQuery, string context)
    {
        ChatHistory history = new();
        history.AddSystemMessage("Summarize the provided context based on the query.");
        history.AddUserMessage($"Context: {context}\n\nQuery: {userQuery}.");

        var result = await _chatCompletionService.GetChatMessageContentAsync(history);
        return result.Content ?? "No summary available";
    }
}

