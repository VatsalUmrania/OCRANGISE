using System;

namespace OCRANGISE.Core.Models
{
    public class RenamingRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public RuleType Type { get; set; }
        public string Pattern { get; set; } = "";
        public string Replacement { get; set; } = "";
        public string Template { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
