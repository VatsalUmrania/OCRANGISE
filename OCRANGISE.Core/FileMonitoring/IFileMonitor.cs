using System;

namespace OCRANGISE.Core.FileMonitoring
{
    public interface IFileMonitor : IDisposable
    {
        event Action<string> FileDetected;
        void StartWatching(string[] paths);
        void StopWatching();
        bool IsWatching { get; }
    }
}
