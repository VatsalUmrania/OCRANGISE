using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.Renaming
{
    public interface IRenamingService
    {
        string GenerateNewName(string extractedText, RenamingRule rule);
        string RenameFile(string originalPath, string newFileName, ConflictResolution resolution = ConflictResolution.AddSuffix);
    }
}
