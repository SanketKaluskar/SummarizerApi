using SummarizerApi.Models;
using System.Security.Claims;

namespace SummarizerApi.Repositories;
public interface IChunkRepository
{
    Task InsertChunkAsync(Chunk item);
    Task EmbedAllAsync();
    Task<List<Chunk>> GetAccessibleChunksAsync(ClaimsPrincipal subject);
}
