using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OCRANGISE.Core.FileMonitoring;
using OCRANGISE.Core.Logging;
using OCRANGISE.Core.Models;
using OCRANGISE.Core.OCR;
using OCRANGISE.Core.Renaming;

namespace OCRANGISE.Core.Pipeline
{
    public class ProcessingPipeline : IDisposable
    {
        private readonly IFileMonitor _fileMonitor;
        private readonly IOcrProcessor _ocrProcessor;
        private readonly IRenamingService _renamingService;
        private readonly IOperationLogger _logger;
        private readonly List<RenamingRule> _rules;
        private readonly object _lockObject = new object();
        private bool _isProcessing = false;
        private int _processedFilesCount = 0;
        private int _failedFilesCount = 0;

        public ProcessingPipeline(
            IFileMonitor? fileMonitor = null,
            IOcrProcessor? ocrProcessor = null,
            IRenamingService? renamingService = null,
            IOperationLogger? logger = null)
        {
            _fileMonitor = fileMonitor ?? new FileSystemWatcherService();
            _ocrProcessor = ocrProcessor ?? new TesseractOcrProcessor();
            _renamingService = renamingService ?? new SmartRenamingService();
            _logger = logger ?? new OperationLogger();
            _rules = new List<RenamingRule>();

            _fileMonitor.FileDetected += OnFileDetected;

            // Add default rules
            InitializeDefaultRules();
        }

        #region Public Methods

        public void StartMonitoring(string[] folderPaths)
        {
            try
            {
                // Validate folder paths
                var validPaths = ValidateFolderPaths(folderPaths);

                if (!validPaths.Any())
                {
                    _logger.LogError("StartMonitoring", "No valid folder paths provided");
                    return;
                }

                _logger.LogSystemStart(validPaths.ToArray());
                _fileMonitor.StartWatching(validPaths.ToArray());
                _isProcessing = true;

                Console.WriteLine($"✅ Monitoring started for {validPaths.Count} folder(s):");
                foreach (var path in validPaths)
                {
                    Console.WriteLine($"   📁 {path}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("StartMonitoring", $"Failed to start monitoring: {ex.Message}");
                throw;
            }
        }

        public void StopMonitoring()
        {
            try
            {
                _isProcessing = false;
                _fileMonitor.StopWatching();
                _logger.LogSystemStop();

                Console.WriteLine($"🛑 Monitoring stopped. Processed: {_processedFilesCount}, Failed: {_failedFilesCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError("StopMonitoring", $"Error stopping monitoring: {ex.Message}");
            }
        }

        public void AddRule(RenamingRule rule)
        {
            lock (_lockObject)
            {
                _rules.Add(rule);
                _logger.LogSuccess("AddRule", $"Rule added: {rule.Name}", "", "System");
            }
        }

        public void RemoveRule(Guid ruleId)
        {
            lock (_lockObject)
            {
                var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
                if (rule != null)
                {
                    _rules.Remove(rule);
                    _logger.LogSuccess("RemoveRule", $"Rule removed: {rule.Name}", "", "System");
                }
            }
        }

        public IReadOnlyList<RenamingRule> GetRules()
        {
            lock (_lockObject)
            {
                return _rules.AsReadOnly();
            }
        }

        public void ProcessFileManually(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError(filePath, "File does not exist");
                Console.WriteLine($"❌ File not found: {filePath}");
                return;
            }

            Console.WriteLine($"🔄 Manual processing: {Path.GetFileName(filePath)}");
            ProcessFile(filePath);
        }

        #endregion

        #region Event Handlers

        private async void OnFileDetected(string filePath)
        {
            if (!_isProcessing) return;

            // Process file asynchronously to avoid blocking UI
            await Task.Run(() => ProcessFile(filePath));
        }

        #endregion

        #region Core Processing

        public void ProcessFile(string filePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var fileName = Path.GetFileName(filePath);

            _logger.LogFileDetected(filePath);
            Console.WriteLine($"\n📄 Processing: {fileName}");

            try
            {
                // Validate file
                if (!ValidateFile(filePath))
                {
                    return;
                }

                // Wait for file to be fully written
                if (!WaitForFileReady(filePath))
                {
                    _logger.LogError(filePath, "File not ready after timeout");
                    Console.WriteLine($"⏰ Timeout waiting for file: {fileName}");
                    return;
                }

                // Check if file type is supported
                if (!_ocrProcessor.IsSupported(filePath))
                {
                    _logger.LogError(filePath, "Unsupported file type");
                    Console.WriteLine($"❌ Unsupported file type: {fileName}");
                    return;
                }

                // Extract text using OCR
                _logger.LogOcrStart(filePath);
                Console.WriteLine($"🔍 Extracting text from: {fileName}");

                var extractedText = _ocrProcessor.ExtractText(filePath);

                stopwatch.Stop();
                _logger.LogOcrComplete(filePath, extractedText?.Length ?? 0, stopwatch.Elapsed);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger.LogError(filePath, "No text extracted from file");
                    Console.WriteLine($"⚠️ No text found in: {fileName}");
                    _failedFilesCount++;
                    return;
                }

                Console.WriteLine($"📝 Extracted text ({extractedText.Length} chars): {TruncateText(extractedText, 100)}");

                // Get active rule
                var rule = GetActiveRule();
                Console.WriteLine($"📋 Applying rule: {rule.Name}");

                // Generate new filename
                var newFileName = _renamingService.GenerateNewName(extractedText, rule);
                Console.WriteLine($"📝 Generated name: {newFileName}");

                // Rename the file
                var newPath = _renamingService.RenameFile(filePath, newFileName);
                var newFileNameOnly = Path.GetFileName(newPath);

                // Log success
                _logger.LogSuccess(filePath, newPath, extractedText, rule.Name);

                Console.WriteLine($"✅ Renamed: {fileName} → {newFileNameOnly}");
                _processedFilesCount++;

                // Show processing time
                Console.WriteLine($"⏱️ Processing completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(filePath, $"{ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"❌ Error processing {fileName}: {ex.Message}");
                _failedFilesCount++;
            }
        }

        #endregion

        #region Helper Methods

        private List<string> ValidateFolderPaths(string[] folderPaths)
        {
            var validPaths = new List<string>();

            foreach (var path in folderPaths)
            {
                if (Directory.Exists(path))
                {
                    validPaths.Add(path);
                }
                else
                {
                    _logger.LogError("ValidateFolderPaths", $"Folder does not exist: {path}");
                    Console.WriteLine($"⚠️ Folder not found: {path}");
                }
            }

            return validPaths;
        }

        private bool ValidateFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError(filePath, "File does not exist");
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogError(filePath, "File is empty");
                    return false;
                }

                if (fileInfo.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    _logger.LogError(filePath, $"File too large: {fileInfo.Length / (1024 * 1024)}MB");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(filePath, $"File validation error: {ex.Message}");
                return false;
            }
        }

        private bool WaitForFileReady(string filePath, int timeoutMs = 5000)
        {
            var timeout = DateTime.Now.AddMilliseconds(timeoutMs);

            while (DateTime.Now < timeout)
            {
                try
                {
                    using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return file.Length > 0;
                }
                catch (IOException)
                {
                    // File is still being written, wait a bit
                    System.Threading.Thread.Sleep(500);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        private RenamingRule GetActiveRule()
        {
            lock (_lockObject)
            {
                return _rules.FirstOrDefault(r => r.IsActive) ?? GetDefaultRule();
            }
        }

        private void InitializeDefaultRules()
        {
            var defaultRules = new[]
            {
                new RenamingRule
                {
                    Name = "Smart Document Detection",
                    Type = RuleType.Smart,
                    IsActive = true
                },
                new RenamingRule
                {
                    Name = "First Line Clean",
                    Type = RuleType.FirstLine,
                    IsActive = false
                },
                new RenamingRule
                {
                    Name = "Invoice Pattern",
                    Type = RuleType.Regex,
                    Pattern = @"Invoice\s*#?\s*(\d+)",
                    Replacement = "Invoice_$1",
                    IsActive = false
                }
            };

            foreach (var rule in defaultRules)
            {
                _rules.Add(rule);
            }

            _logger.LogSuccess("InitializeDefaultRules", "Default rules initialized", "", "System");
        }

        private RenamingRule GetDefaultRule()
        {
            return new RenamingRule
            {
                Name = "Fallback - First Line",
                Type = RuleType.FirstLine,
                IsActive = true
            };
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }

        #endregion

        #region Statistics and Monitoring

        public void PrintStatistics()
        {
            Console.WriteLine("\n📊 Processing Statistics:");
            Console.WriteLine($"   ✅ Processed: {_processedFilesCount}");
            Console.WriteLine($"   ❌ Failed: {_failedFilesCount}");
            Console.WriteLine($"   📋 Active Rules: {_rules.Count(r => r.IsActive)}");
            Console.WriteLine($"   🔄 Status: {(_isProcessing ? "Monitoring" : "Stopped")}");
        }

        public bool IsMonitoring => _isProcessing && _fileMonitor.IsWatching;

        public int ProcessedFilesCount => _processedFilesCount;
        public int FailedFilesCount => _failedFilesCount;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                StopMonitoring();
                _fileMonitor?.Dispose();
                OperationLogger.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disposal: {ex.Message}");
            }
        }

        #endregion
    }
}
