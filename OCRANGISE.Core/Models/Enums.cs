using System;

namespace OCRANGISE.Core.Models
{
    public enum RuleType
    {
        Smart,           // AI-like document type detection
        FirstLine,       // Clean first meaningful line
        Regex,           // Custom regex patterns
        Template,        // Template-based naming
        DateBased,       // Date-focused naming
        DocumentType,    // Type-specific rules
        ContentBased     // Based on document content analysis
    }

    public enum ConflictResolution
    {
        AddSuffix,       // Add _001, _002, etc.
        AddTimestamp,    // Add timestamp
        Overwrite,       // Replace existing
        Skip,            // Keep original name
        Interactive      // Ask user (for GUI mode)
    }


    public enum FileType
    {
        Image,
        PDF,
        Unknown
    }
}
