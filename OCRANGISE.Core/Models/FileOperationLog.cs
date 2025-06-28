using System;

namespace OCRANGISE.Core.Models
{
    public class FileOperationLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string OriginalPath { get; set; } = "";
        public string NewPath { get; set; } = "";
        public string ExtractedText { get; set; } = "";
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string RuleUsed { get; set; } = "";
    }
}
