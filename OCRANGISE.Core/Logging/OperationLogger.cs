using System;
using System.IO;
using Serilog;
using Serilog.Events;
using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.Logging
{
    public class OperationLogger : IOperationLogger
    {
        private static readonly ILogger _logger;

        static OperationLogger()
        {
            // Create logs directory if it doesn't exist
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            _logger = new LoggerConfiguration()
                // Console output for debugging
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")

                // File output with rolling
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "ocrangise-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 50_000_000, // 50MB per file
                    rollOnFileSizeLimit: true,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")

                // Error-only file for critical issues
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")

                // Set minimum level
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)

                // Enrich with additional context
                .Enrich.WithProperty("Application", "OCRANGISE")
                .Enrich.WithProperty("Version", "1.0.0")

                .CreateLogger();
        }

        public void LogOperation(FileOperationLog log)
        {
            if (log.Success)
            {
                _logger.Information("File operation completed successfully: {OriginalPath} -> {NewPath} | Rule: {RuleUsed} | Text: {ExtractedText}",
                    log.OriginalPath, log.NewPath, log.RuleUsed, TruncateText(log.ExtractedText, 100));
            }
            else
            {
                _logger.Error("File operation failed: {OriginalPath} | Error: {ErrorMessage}",
                    log.OriginalPath, log.ErrorMessage);
            }
        }

        public void LogSuccess(string oldPath, string newPath, string extractedText, string ruleUsed)
        {
            _logger.Information("OCR Processing Success: {OriginalFile} renamed to {NewFile} using rule '{Rule}' | Extracted: {Text}",
                Path.GetFileName(oldPath), Path.GetFileName(newPath), ruleUsed, TruncateText(extractedText, 50));
        }

        public void LogError(string path, string error)
        {
            _logger.Error("OCR Processing Error: Failed to process {FilePath} | Error: {ErrorMessage}",
                path, error);
        }

        public void LogFileDetected(string filePath)
        {
            _logger.Debug("File detected: {FilePath}", filePath);
        }

        public void LogOcrStart(string filePath)
        {
            _logger.Information("Starting OCR extraction for: {FilePath}", filePath);
        }

        public void LogOcrComplete(string filePath, int textLength, TimeSpan duration)
        {
            _logger.Information("OCR extraction completed for {FilePath} | Text length: {TextLength} chars | Duration: {Duration}ms",
                Path.GetFileName(filePath), textLength, duration.TotalMilliseconds);
        }

        public void LogSystemStart(string[] monitoredPaths)
        {
            _logger.Information("OCRANGISE monitoring started for paths: {MonitoredPaths}", monitoredPaths);
        }

        public void LogSystemStop()
        {
            _logger.Information("OCRANGISE monitoring stopped");
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
