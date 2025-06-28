using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OCRANGISE.Core.FileMonitoring
{
    public class FileSystemWatcherService : IFileMonitor, IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".tiff", ".bmp", ".pdf" };

        public event Action<string>? FileDetected;
        public bool IsWatching => _watchers.Any(w => w.EnableRaisingEvents);

        public void StartWatching(string[] paths)
        {
            StopWatching();

            foreach (var path in paths.Where(Directory.Exists))
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnFileCreated;
                _watchers.Add(watcher);
            }
        }

        public void StopWatching()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (IsSupportedFile(e.FullPath))
            {
                // Wait longer for file to be fully written
                System.Threading.Thread.Sleep(2000); // Increased from 500ms

                // Check if file is ready before processing
                if (IsFileReady(e.FullPath))
                {
                    FileDetected?.Invoke(e.FullPath);
                }
            }
        }

        private bool IsFileReady(string filePath)
        {
            try
            {
                using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return file.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }


        private bool IsSupportedFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return _supportedExtensions.Contains(extension);
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
