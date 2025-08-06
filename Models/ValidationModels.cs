using System.Collections.Generic;

namespace SemanticKernelAgent.Models
{
    public class ValidationResult
    {
        public string ValidationFeedback { get; set; } = "";
        public bool HasIssues { get; set; }
        public string OriginalTask { get; set; } = "";
        public string TaskResult { get; set; } = "";
        public List<string> SuggestedImprovements { get; set; } = new();
    }

    public class ValidationConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public bool UseGemini { get; set; } = true;
    }
}
