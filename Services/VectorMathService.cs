namespace SummarizerApi.Services;
public class VectorMathService : IVectorMathService
{
    public float GetCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            return 0f;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        normA = Math.Sqrt(normA);
        normB = Math.Sqrt(normB);

        if (normA == 0 || normB == 0)
        {
            return 0f;
        }

        return (float)(dotProduct / (normA * normB));
    }
}