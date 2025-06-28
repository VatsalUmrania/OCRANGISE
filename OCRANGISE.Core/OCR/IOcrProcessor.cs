namespace OCRANGISE.Core.OCR
{
    public interface IOcrProcessor
    {
        string ExtractText(string filePath);
        bool IsSupported(string filePath);
    }
}
