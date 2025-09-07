using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SummarizerApi.Models;
public class Chunk
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public List<string> RequiredRoles { get; set; } = [];
    public double[] Embedding { get; set; } = [];
}

