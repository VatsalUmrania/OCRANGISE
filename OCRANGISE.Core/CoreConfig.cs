using System;
using System.IO;
using OCRANGISE.Core.Models;  // Add this line

namespace OCRANGISE.Core
{
    public static class CoreConfig
    {
        public static string TessdataPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        public static string DefaultLanguage { get; set; } = "eng";
        public static string LogDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        public static int LogRetentionDays { get; set; } = 30;
        public static ConflictResolution DefaultConflictResolution { get; set; } = ConflictResolution.AddSuffix;
    }
}
