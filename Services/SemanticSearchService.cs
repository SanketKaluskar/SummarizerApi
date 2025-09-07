using Microsoft.AspNetCore.DataProtection.KeyManagement;
using SummarizerApi.Controllers;
using SummarizerApi.Models;

namespace SummarizerApi.Services;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly ILLMService _llmService;
    private readonly IVectorMathService _vectorMathService;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        ILLMService llmService, 
        IVectorMathService vectorMathService, 
        ILogger<SemanticSearchService> logger)
    {
        _llmService = llmService;
        _vectorMathService = vectorMathService;
        _logger = logger;
    }

    public async Task<List<Chunk>> SearchAsync(string query, List<Chunk> chunks)
    {
        // Generate query embedding
        var queryEmbedding = await _llmService.GenerateEmbeddingAsync(query);

        // Compute cosine similarity between each chunk and the query
        // and compare against a minimum relevance score (from configuration)

        //  Scores close to 1: Highly relevant/semantically similar
        //  Scores around 0.5: Weak or partial relevance
        //  Scores below 0.3: Likely unrelated

        // Set to a low value given my crummy embedding generator
        const float MinimumRelevanceScore = 0.3f;

        var scoredChunks = chunks
            .Where(c => c.Embedding != null && c.Embedding.Length > 0)
            .Select(c => new
            {
                Chunk = c,
                Score = _vectorMathService.GetCosineSimilarity(Array.ConvertAll(c.Embedding, x => (float)x), queryEmbedding)
            })
            .OrderByDescending(x => x.Score)
            .Where(x => x.Score >= MinimumRelevanceScore)
            .Select(x => (x.Chunk, x.Score))
            .ToList();

        foreach (var pair in scoredChunks)
        {
            _logger.LogInformation($"ChunkId:{pair.Chunk.Id} Score:{pair.Score}");
        }

        return scoredChunks.Select(x => x.Chunk).ToList(); ;
    }
}

