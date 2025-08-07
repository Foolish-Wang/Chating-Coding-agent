using System;
using System.Collections.Generic;
using System.Linq;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// RAG 工具函数
    /// </summary>
    public static class RagUtils
    {
        /// <summary>
        /// 计算向量余弦相似度
        /// </summary>
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("向量维度不匹配");

            var dotProduct = vectorA.Zip(vectorB, (a, b) => a * b).Sum();
            var magnitudeA = Math.Sqrt(vectorA.Sum(a => a * a));
            var magnitudeB = Math.Sqrt(vectorB.Sum(b => b * b));

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return (float)(dotProduct / (magnitudeA * magnitudeB));
        }

        /// <summary>
        /// 生成唯一的 chunk ID
        /// </summary>
        public static string GenerateChunkId(string fileName, int chunkIndex)
        {
            return $"{fileName}_{chunkIndex}_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 格式化向量输出
        /// </summary>
        public static string FormatVector(float[] vector, int displayLength = 5)
        {
            if (vector == null || vector.Length == 0)
                return "[]";

            var displayVector = vector.Take(displayLength);
            return $"[{string.Join(", ", displayVector.Select(v => v.ToString("F3")))}...]";
        }
    }
}
