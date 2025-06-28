using System;
using OCRANGISE.Core.Pipeline;
using OCRANGISE.Core.Models;

// Create and configure the pipeline
var pipeline = new ProcessingPipeline();

// Add custom rules
pipeline.AddRule(new RenamingRule
{
    Name = "Invoice Rule",
    Type = RuleType.Regex,
    Pattern = @"Invoice\s+(\d+)",
    Replacement = "INV_$1"
});

// Start monitoring folders
pipeline.StartMonitoring(new[] { @"D:\Documents\Scanned"});

Console.WriteLine("Monitoring started. Press any key to stop...");
Console.ReadKey();

pipeline.StopMonitoring();
pipeline.Dispose();
