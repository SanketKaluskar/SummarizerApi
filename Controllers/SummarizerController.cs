using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SummarizerApi.Models;
using SummarizerApi.Repositories;
using SummarizerApi.Services;
using System.Diagnostics;

namespace SummarizerApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class SummarizerController : ControllerBase
{
    private readonly IChunkRepository _chunkRepository;
    private readonly ILLMService _llmService;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly ILogger<SummarizerController> _logger;

    public SummarizerController(
        IChunkRepository chunkRepository, 
        ILLMService llmService, 
        ISemanticSearchService semanticSearchService,
        ILogger<SummarizerController> logger)
    {
        _chunkRepository = chunkRepository;
        _llmService = llmService;
        _semanticSearchService = semanticSearchService;
        _logger = logger;
    }

    [HttpPost("query")]
    public async Task<IActionResult> QueryLlm([FromBody] QueryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { Error = "Query is required." });
        }

        try
        {
            // Find accessible chunks by applying AuthZ rules, (Subject, Scope, Resource) tuple.
            // cf. Google Zanzibar, Auth0 FGA
            
            // AuthZ occurs close to the resource (respository, in this case)
            // and uses biz rules like:
            //  Agents don't have access to special projects
            //  Administrators have access to everything in their and other orgs
            //  Workers, Managers (and their delegated Agents) don't have cross-org access
            // Rules to prevent cross-tenant leakage go here.

            List<Chunk> accessibleChunks = await _chunkRepository.GetAccessibleChunksAsync(User);
            _logger.LogInformation($"Accessible Chunks:{accessibleChunks.Count}");
            
            if (accessibleChunks.Count == 0)
            {
                return Ok(new { Response = "You do not have access to any relevant data." });
            }

            // Prune what is sent to the LLM. This accomplishes the following objectives:
            //  Limits operating costs, as LLMs are metered by input/output tokens
            //  Promotes grounded LLM output, as the input is relevant (semantically similar to the query)
            //  Limits LLM latency, due to a smaller prompt

            // Semantic search is nearly free while LLM is not:
            //  Operating cost of query embedding is orders of magnitude less than prompt cost
            //  Embedding for chunks is a onetime per-chunk cost
            //  Chunk retrieval (repository I/O) and computing chunk vs. query similarity (compute) is nearly free

            List<Chunk> scoredChunks = await _semanticSearchService.SearchAsync(request.Query, accessibleChunks);
            _logger.LogInformation($"Relevant Chunks:{scoredChunks.Count}");

            if (scoredChunks.Count == 0)
            {
                return Ok(new { Response = "You do not have access to any relevant data." });
            }

            // Build context from top N (configurable) relevant chunks
            const int TopN = 5;
            List<string> filteredContexts = scoredChunks.Take(TopN).Select(x => x.Content).ToList();
            string context = string.Join("\n\n", filteredContexts);

            _logger.LogInformation($"Query:\"{request.Query}\"");
            _logger.LogInformation($"Context:\"{context}\"");

            // Call LLM to summarize the context based on query
            Stopwatch sw = Stopwatch.StartNew();
            string response = await _llmService.GetLLMResponseAsync(request.Query, context);
            sw.Stop();
            _logger.LogInformation($"LLM responsed in {sw.ElapsedMilliseconds:N0} ms");
            _logger.LogInformation($"Response:\"{response}\"");

            return Ok(new { Response = response });
        }
        finally
        {
            _logger.LogInformation("End of Response\n");
        }
    }

    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate()
    {
        await _chunkRepository.EmbedAllAsync();
        return Ok("Migration completed");
    }
}

