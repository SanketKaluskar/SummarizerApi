using SummarizerApi.Models;

namespace SummarizerApi.Services;
public interface ISemanticSearchService
{
    Task<List<Chunk>> SearchAsync(string query, List<Chunk> chunks);
}