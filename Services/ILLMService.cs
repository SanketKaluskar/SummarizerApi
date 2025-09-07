namespace SummarizerApi.Services;
public interface ILLMService
{
    Task<string> GetLLMResponseAsync(string userQuery, string context);
    Task<float[]> GenerateEmbeddingAsync(string text);
}
