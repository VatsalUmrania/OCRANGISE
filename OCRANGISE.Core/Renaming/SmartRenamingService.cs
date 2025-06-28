using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.Renaming
{
    public class SmartRenamingService : IRenamingService
    {
        public string GenerateNewName(string extractedText, RenamingRule rule)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
                return GenerateFallbackName();

            var cleanedText = CleanExtractedText(extractedText);
            return DetectDocumentTypeAndRename(cleanedText);
        }

        public string RenameFile(string originalPath, string newFileName, ConflictResolution resolution = ConflictResolution.AddSuffix)
        {
            var directory = Path.GetDirectoryName(originalPath) ?? "";
            var extension = Path.GetExtension(originalPath);
            var sanitizedName = SanitizeFileName(newFileName);
            var newPath = Path.Combine(directory, sanitizedName + extension);

            if (File.Exists(newPath))
            {
                newPath = resolution switch
                {
                    ConflictResolution.AddSuffix => GenerateUniqueFileName(directory, sanitizedName, extension),
                    ConflictResolution.Skip => originalPath,
                    ConflictResolution.Overwrite => newPath,
                    _ => throw new ArgumentException("Unsupported conflict resolution")
                };
            }

            if (newPath != originalPath)
            {
                File.Move(originalPath, newPath);
            }

            return newPath;
        }

        private string DetectDocumentTypeAndRename(string text)
        {
            // Invoice detection
            if (IsInvoice(text))
            {
                return ExtractInvoiceInfo(text);
            }

            // Receipt detection
            if (IsReceipt(text))
            {
                return ExtractReceiptInfo(text);
            }

            // Letter/Email detection
            if (IsLetter(text))
            {
                return ExtractLetterInfo(text);
            }

            // Fallback to meaningful first line
            return GetMeaningfulName(text);
        }

        private bool IsInvoice(string text)
        {
            var invoiceKeywords = new[] { "invoice", "bill", "amount due", "total", "payment", "due date" };
            return invoiceKeywords.Any(keyword => text.ToLower().Contains(keyword));
        }

        private bool IsReceipt(string text)
        {
            var receiptKeywords = new[] { "receipt", "thank you", "purchase", "transaction" };
            return receiptKeywords.Any(keyword => text.ToLower().Contains(keyword));
        }

        private bool IsLetter(string text)
        {
            var letterKeywords = new[] { "dear", "sincerely", "regards", "yours truly" };
            return letterKeywords.Any(keyword => text.ToLower().Contains(keyword));
        }

        private string ExtractInvoiceInfo(string text)
        {
            var invoiceMatch = Regex.Match(text, @"Invoice\s*#?\s*(\w+)", RegexOptions.IgnoreCase);
            var invoiceNumber = invoiceMatch.Success ? invoiceMatch.Groups[1].Value : "INV";
            var vendor = ExtractCompanyName(text);
            var date = ExtractDate(text);
            return $"Invoice_{invoiceNumber}_{vendor}_{date}";
        }

        private string ExtractReceiptInfo(string text)
        {
            var vendor = ExtractCompanyName(text);
            var amount = ExtractAmount(text);
            var date = ExtractDate(text);
            return $"Receipt_{vendor}_{date}_{amount}";
        }

        private string ExtractLetterInfo(string text)
        {
            var recipient = ExtractRecipient(text);
            var date = ExtractDate(text);
            return $"Letter_{recipient}_{date}";
        }

        private string ExtractCompanyName(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Take(5))
            {
                var cleanLine = line.Trim();

                if (IsHeaderLine(cleanLine)) continue;

                if (cleanLine.Length > 3 && cleanLine.Length < 50 &&
                    Regex.IsMatch(cleanLine, @"^[A-Z][a-zA-Z\s&.,'-]+$"))
                {
                    return SanitizeFileName(cleanLine);
                }
            }

            return "Company";
        }

        private string ExtractRecipient(string text)
        {
            var dearMatch = Regex.Match(text, @"Dear\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)", RegexOptions.IgnoreCase);
            if (dearMatch.Success)
            {
                return SanitizeFileName(dearMatch.Groups[1].Value);
            }
            return "Recipient";
        }

        private string ExtractDate(string text)
        {
            var datePatterns = new[]
            {
                @"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
                @"(\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})",
                @"((?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2},?\s+\d{4})"
            };

            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
                {
                    return date.ToString("yyyy-MM-dd");
                }
            }

            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        private string ExtractAmount(string text)
        {
            var amountPatterns = new[]
            {
                @"\$\s*(\d+(?:,\d{3})*(?:\.\d{2})?)",
                @"Total[:\s]+\$?\s*(\d+(?:,\d{3})*(?:\.\d{2})?)"
            };

            foreach (var pattern in amountPatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Replace(",", "");
                }
            }

            return "";
        }

        private bool IsHeaderLine(string line)
        {
            var headerPatterns = new[]
            {
                @"^\d+$",
                @"^Page\s+\d+",
                @"^\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}$"
            };

            return headerPatterns.Any(pattern => Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase));
        }

        private string GetMeaningfulName(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();

                if (cleanLine.Length > 5 && cleanLine.Length < 80 &&
                    !IsHeaderLine(cleanLine) &&
                    HasMeaningfulContent(cleanLine))
                {
                    return SanitizeFileName(cleanLine.Substring(0, Math.Min(cleanLine.Length, 50)));
                }
            }

            return $"Document_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private bool HasMeaningfulContent(string text)
        {
            return Regex.IsMatch(text, @"[a-zA-Z]") &&
                   !Regex.IsMatch(text, @"^[\d\s\-\/\.]+$");
        }

        private string CleanExtractedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "Document";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            return string.IsNullOrWhiteSpace(sanitized) ? "Document" :
                   sanitized.Substring(0, Math.Min(sanitized.Length, 100));
        }

        private string GenerateUniqueFileName(string directory, string baseName, string extension)
        {
            var counter = 1;
            string newPath;
            do
            {
                var fileName = $"{baseName}_{counter:D3}{extension}";
                newPath = Path.Combine(directory, fileName);
                counter++;
            } while (File.Exists(newPath) && counter < 1000);

            return newPath;
        }

        private string GenerateFallbackName()
        {
            return $"Document_{DateTime.Now:yyyyMMdd_HHmmss}";
        }
    }
}
