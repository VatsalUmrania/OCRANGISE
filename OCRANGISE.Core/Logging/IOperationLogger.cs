using System;
using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.Logging
{
    public interface IOperationLogger
    {
        void LogOperation(FileOperationLog log);
        void LogSuccess(string oldPath, string newPath, string extractedText, string ruleUsed);
        void LogError(string path, string error);
        void LogFileDetected(string filePath);
        void LogOcrStart(string filePath);
        void LogOcrComplete(string filePath, int textLength, TimeSpan duration);
        void LogSystemStart(string[] monitoredPaths);
        void LogSystemStop();
    }
}
