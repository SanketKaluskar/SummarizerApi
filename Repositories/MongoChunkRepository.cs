using MongoDB.Driver;
using SummarizerApi.Models;
using SummarizerApi.Services;
using System.Security.Claims;

namespace SummarizerApi.Repositories;

public class MongoChunkRepository : IChunkRepository
{
    private readonly IMongoCollection<Chunk> _chunks;
    private readonly ILLMService _llmService;
    private readonly ILogger<MongoChunkRepository> _logger;

    public MongoChunkRepository(
        IMongoClient client,
        ILLMService llmService,
        ILogger<MongoChunkRepository> logger)
    {
        _chunks = client.GetDatabase("LlmDataDb").GetCollection<Chunk>("Chunks");
        _llmService = llmService;
        _logger = logger;
    }

    public async Task InsertChunkAsync(Chunk item)
    {
        // Generate embedding for a new chunk
        float[] embedding = await _llmService.GenerateEmbeddingAsync(item.Content);
        item.Embedding = Array.ConvertAll(embedding, x => (double)x);
        await _chunks.InsertOneAsync(item);
    }

    public async Task EmbedAllAsync()
    {
        // Generate embedding for existing chunks
        // Useful when you've migrated chunks (sans embedding) into the repository
        var allData = await _chunks.Find(Builders<Chunk>.Filter.Empty).ToListAsync();
        foreach (var item in allData)
        {
            if (item.Embedding == null || item.Embedding.Length == 0)
            {
                var embedding = await _llmService.GenerateEmbeddingAsync(item.Content);
                var update = Builders<Chunk>.Update.Set(c => c.Embedding, Array.ConvertAll(embedding, x => (double)x));
                await _chunks.UpdateOneAsync(c => c.Id == item.Id, update);
            }
        }
    }

    public async Task<List<Chunk>> GetAccessibleChunksAsync(ClaimsPrincipal subject)
    {
        var subjScope = subject.FindFirst("scope")?.Value ?? string.Empty;
        _logger.LogInformation($"Scope:\"{subjScope}\"");

        if (subjScope != "Files.Read")
        {
            return []; // Insufficient scope
        }

        // Subject claims
        var subjRolesClaim = subject.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;
        var subjRoles = subjRolesClaim?.Split(',').ToList() ?? new List<string>();
        var subjOrg = subject.FindFirst("organization")?.Value ?? string.Empty;
        var subjProject = subject.FindFirst("project")?.Value ?? string.Empty;

        // Actor claims
        var actor = ((ClaimsIdentity)subject.Identity)?.Actor;
        var actOrg = actor?.Claims.FirstOrDefault(claim => claim.Type == "organization", null)?.Value ?? string.Empty;
        var actIdentityType = actor?.Claims.FirstOrDefault(claim => claim.Type == "identitytype", null)?.Value ?? string.Empty;

        _logger.LogInformation($"Roles:\"{subjRolesClaim}\" SubjectOrg:\"{subjOrg}\" SubjectProject:\"{subjProject}\" ActorOrg:\"{actOrg}\" ActorIdentityType:\"{actIdentityType}\"");

        if (subjOrg != string.Empty && actOrg != string.Empty && subjOrg != actOrg)
        {
            return []; // Actor and Subject from different organizations (broken invariant)
        }

        // Administrator role has access to all chunks.
        if (subjRoles.Contains("Administrator"))
        {
            FilterDefinition<Chunk> adminFilter;
            if (actIdentityType == "agent")
            {
                // Agents are denied access to special projects.
                adminFilter = Builders<Chunk>.Filter.Eq(c => c.Project, string.Empty);
            }
            else
            {
                adminFilter = Builders<Chunk>.Filter.Empty;
            }

            return await _chunks.Find(adminFilter).ToListAsync();
        }

        if (!subjRoles.Contains("Manager") && !subjRoles.Contains("Worker"))
        {
            return []; // Unknown role, access denied.
        }

        // Chunks allowed for subject's roles
        FilterDefinition<Chunk> filter = Builders<Chunk>.Filter.AnyIn(c => c.RequiredRoles, subjRoles);

        // from their organization
        filter = Builders<Chunk>.Filter.And(Builders<Chunk>.Filter.Eq(c => c.Organization, subjOrg), filter);
        
        // and their project, except for agentic actors
        if (actIdentityType == "agent")
        {
            // Agents are denied access to special projects.
            filter = Builders<Chunk>.Filter.And(Builders<Chunk>.Filter.Eq(c => c.Project, string.Empty), filter);
        }
        else if (!subjRoles.Contains("Manager")) // Manager role has access to all projects in their org.
        {
            filter = Builders<Chunk>.Filter.And(Builders<Chunk>.Filter.Eq(c => c.Project, subjProject), filter);
        }

        return await _chunks.Find(filter).ToListAsync();
    }
}