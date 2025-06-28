using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
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
        private readonly Dispatcher? _dispatcher;
        private bool _isProcessing = false;
        private volatile int _processedFilesCount = 0;
        private volatile int _failedFilesCount = 0;

        // Events for UI updates
        public event Action<string, string>? FileProcessed;
        public event Action<string, string>? ProcessingFailed;

        public ProcessingPipeline(
            Dispatcher? dispatcher = null,
            IFileMonitor? fileMonitor = null,
            IOcrProcessor? ocrProcessor = null,
            IRenamingService? renamingService = null,
            IOperationLogger? logger = null)
        {
            _dispatcher = dispatcher;
            _fileMonitor = fileMonitor ?? new FileSystemWatcherService();
            _ocrProcessor = ocrProcessor ?? new TesseractOcrProcessor();
            _renamingService = renamingService ?? new SmartRenamingService();
            _logger = logger ?? new OperationLogger();
            _rules = new List<RenamingRule>();

            _fileMonitor.FileDetected += OnFileDetectedAsync;
            InitializeDefaultRules();
        }

        #region Public Properties

        public int ProcessedFilesCount => _processedFilesCount;
        public int FailedFilesCount => _failedFilesCount;
        public bool IsMonitoring => _isProcessing && _fileMonitor.IsWatching;

        #endregion

        #region Public Methods

        public void StartMonitoring(string[] folderPaths)
        {
            try
            {
                var validPaths = ValidateFolderPaths(folderPaths);

                if (!validPaths.Any())
                {
                    _logger.LogError("StartMonitoring", "No valid folder paths provided");
                    return;
                }

                _logger.LogSystemStart(validPaths.ToArray());
                _fileMonitor.StartWatching(validPaths.ToArray());
                _isProcessing = true;

                LogToUI($"✅ Monitoring started for {validPaths.Count} folder(s)");
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

                LogToUI($"🛑 Monitoring stopped. Processed: {_processedFilesCount}, Failed: {_failedFilesCount}");
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
                LogToUI($"❌ File not found: {filePath}");
                return;
            }

            LogToUI($"🔄 Manual processing: {Path.GetFileName(filePath)}");
            Task.Run(() => ProcessFileAsync(filePath));
        }

        #endregion

        #region Event Handlers

        private async void OnFileDetectedAsync(string filePath)
        {
            if (!_isProcessing) return;
            await ProcessFileAsync(filePath);
        }

        #endregion

        #region Core Processing

        private async Task ProcessFileAsync(string filePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var fileName = Path.GetFileName(filePath);

            _logger.LogFileDetected(filePath);
            LogToUI($"📄 Processing: {fileName}");

            try
            {
                // Validate file
                if (!await ValidateFileAsync(filePath))
                {
                    return;
                }

                // Wait for file to be ready
                if (!await WaitForFileReadyAsync(filePath))
                {
                    _logger.LogError(filePath, "File not ready after timeout");
                    LogToUI($"⏰ Timeout waiting for file: {fileName}");
                    System.Threading.Interlocked.Increment(ref _failedFilesCount);
                    ProcessingFailed?.Invoke(filePath, "File timeout");
                    return;
                }

                // Check if file type is supported
                if (!_ocrProcessor.IsSupported(filePath))
                {
                    _logger.LogError(filePath, "Unsupported file type");
                    LogToUI($"❌ Unsupported file type: {fileName}");
                    System.Threading.Interlocked.Increment(ref _failedFilesCount);
                    ProcessingFailed?.Invoke(filePath, "Unsupported file type");
                    return;
                }

                // Extract text using OCR
                _logger.LogOcrStart(filePath);
                LogToUI($"🔍 Extracting text from: {fileName}");

                var extractedText = await Task.Run(() => _ocrProcessor.ExtractText(filePath));

                stopwatch.Stop();
                _logger.LogOcrComplete(filePath, extractedText?.Length ?? 0, stopwatch.Elapsed);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger.LogError(filePath, "No text extracted from file");
                    LogToUI($"⚠️ No text found in: {fileName}");
                    System.Threading.Interlocked.Increment(ref _failedFilesCount);
                    ProcessingFailed?.Invoke(filePath, "No text extracted");
                    return;
                }

                LogToUI($"📝 Extracted text ({extractedText.Length} chars): {TruncateText(extractedText, 100)}");

                // Get active rule
                var rule = GetActiveRule();
                LogToUI($"📋 Applying rule: {rule.Name}");

                // Generate new filename
                var newFileName = _renamingService.GenerateNewName(extractedText, rule);
                LogToUI($"📝 Generated name: {newFileName}");

                // Rename the file
                var newPath = _renamingService.RenameFile(filePath, newFileName);
                var newFileNameOnly = Path.GetFileName(newPath);

                // Log success
                _logger.LogSuccess(filePath, newPath, extractedText, rule.Name);

                LogToUI($"✅ Renamed: {fileName} → {newFileNameOnly}");
                System.Threading.Interlocked.Increment(ref _processedFilesCount);

                // Fire success event
                FileProcessed?.Invoke(filePath, newPath);

                // Show processing time
                LogToUI($"⏱️ Processing completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(filePath, $"{ex.GetType().Name}: {ex.Message}");
                LogToUI($"❌ Error processing {fileName}: {ex.Message}");
                System.Threading.Interlocked.Increment(ref _failedFilesCount);
                ProcessingFailed?.Invoke(filePath, ex.Message);
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
                    LogToUI($"⚠️ Folder not found: {path}");
                }
            }

            return validPaths;
        }

        private async Task<bool> ValidateFileAsync(string filePath)
        {
            return await Task.Run(() =>
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
            });
        }

        private async Task<bool> WaitForFileReadyAsync(string filePath, int timeoutMs = 5000)
        {
            return await Task.Run(() =>
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
                        System.Threading.Thread.Sleep(500);
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }

                return false;
            });
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
            var defaultRule = new RenamingRule
            {
                Name = "Smart Document Detection",
                Type = RuleType.FirstLine,
                IsActive = true
            };

            _rules.Add(defaultRule);
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

        private void LogToUI(string message)
        {
            try
            {
                if (_dispatcher != null)
                {
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    }));
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                }
            }
            catch
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
        }

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
                LogToUI($"Error during disposal: {ex.Message}");
            }
        }

        #endregion
    }
}
