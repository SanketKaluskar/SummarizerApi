namespace SummarizerApi.Services;
public interface IVectorMathService
{
    float GetCosineSimilarity(float[] vectorA, float[] vectorB);
}
