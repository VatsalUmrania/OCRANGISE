//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//using OCRANGISE.Core.Models;

//namespace OCRANGISE.Core.Renaming
//{
//    public class AdvancedRenamingService : IRenamingService
//    {
//        private readonly Dictionary<string, Func<string, string>> _smartRules;
//        private readonly List<string> _stopWords = new() { "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by" };

//        public AdvancedRenamingService()
//        {
//            _smartRules = InitializeSmartRules();
//        }

//        public string GenerateNewName(string extractedText, RenamingRule rule)
//        {
//            if (string.IsNullOrWhiteSpace(extractedText))
//                return GenerateFallbackName();

//            var cleanedText = CleanExtractedText(extractedText);

//            return rule.Type switch
//            {
//                RuleType.Smart => ApplySmartRenaming(cleanedText),
//                RuleType.FirstLine => GetFirstLineClean(cleanedText),
//                RuleType.Regex => ApplyRegexRule(cleanedText, rule.Pattern, rule.Replacement),
//                RuleType.Template => ApplyTemplate(cleanedText, rule.Template),
//                RuleType.DateBased => GenerateDateBasedName(cleanedText),
//                RuleType.DocumentType => DetectAndRenameByType(cleanedText),
//                _ => GetFirstLineClean(cleanedText)
//            };
//        }

//        private string ApplySmartRenaming(string text)
//        {
//            // Try each smart rule until one succeeds
//            foreach (var rule in _smartRules)
//            {
//                var result = rule.Value(text);
//                if (!string.IsNullOrEmpty(result) && result != "Unknown")
//                {
//                    return SanitizeFileName(result);
//                }
//            }

//            // Fallback to first meaningful line
//            return GetFirstLineClean(text);
//        }

//        private Dictionary<string, Func<string, string>> InitializeSmartRules()
//        {
//            return new Dictionary<string, Func<string, string>>
//            {
//                ["Invoice"] = ExtractInvoiceInfo,
//                ["Receipt"] = ExtractReceiptInfo,
//                ["Contract"] = ExtractContractInfo,
//                ["Letter"] = ExtractLetterInfo,
//                ["Report"] = ExtractReportInfo,
//                ["Certificate"] = ExtractCertificateInfo,
//                ["Form"] = ExtractFormInfo,
//                ["Email"] = ExtractEmailInfo
//            };
//        }

//        #region Smart Extraction Methods

//        private string ExtractInvoiceInfo(string text)
//        {
//            // Invoice patterns
//            var patterns = new[]
//            {
//                @"Invoice\s*#?\s*(\d+)",
//                @"INV\s*-?\s*(\d+)",
//                @"Bill\s*#?\s*(\d+)",
//                @"Invoice\s+Number\s*:?\s*(\w+)"
//            };

//            foreach (var pattern in patterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
//                if (match.Success)
//                {
//                    var vendor = ExtractVendorName(text);
//                    var date = ExtractDate(text);
//                    return $"Invoice_{match.Groups[1].Value}_{vendor}_{date}";
//                }
//            }

//            return "";
//        }

//        private string ExtractReceiptInfo(string text)
//        {
//            var vendor = ExtractVendorName(text);
//            var date = ExtractDate(text);
//            var amount = ExtractAmount(text);

//            return $"Receipt_{vendor}_{date}_{amount}";
//        }

//        private string ExtractContractInfo(string text)
//        {
//            var contractPatterns = new[]
//            {
//                @"Contract\s+#?\s*(\w+)",
//                @"Agreement\s+#?\s*(\w+)",
//                @"Contract\s+Number\s*:?\s*(\w+)"
//            };

//            foreach (var pattern in contractPatterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
//                if (match.Success)
//                {
//                    var date = ExtractDate(text);
//                    return $"Contract_{match.Groups[1].Value}_{date}";
//                }
//            }

//            var parties = ExtractParties(text);
//            var date = ExtractDate(text);
//            return $"Contract_{parties}_{date}";
//        }

//        private string ExtractLetterInfo(string text)
//        {
//            var recipient = ExtractRecipient(text);
//            var subject = ExtractSubject(text);
//            var date = ExtractDate(text);

//            return $"Letter_{recipient}_{subject}_{date}";
//        }

//        private string ExtractReportInfo(string text)
//        {
//            var reportType = ExtractReportType(text);
//            var date = ExtractDate(text);
//            var period = ExtractPeriod(text);

//            return $"Report_{reportType}_{period}_{date}";
//        }

//        private string ExtractCertificateInfo(string text)
//        {
//            var certType = ExtractCertificateType(text);
//            var name = ExtractPersonName(text);
//            var date = ExtractDate(text);

//            return $"Certificate_{certType}_{name}_{date}";
//        }

//        private string ExtractFormInfo(string text)
//        {
//            var formType = ExtractFormType(text);
//            var formNumber = ExtractFormNumber(text);
//            var date = ExtractDate(text);

//            return $"Form_{formType}_{formNumber}_{date}";
//        }

//        private string ExtractEmailInfo(string text)
//        {
//            var sender = ExtractEmailSender(text);
//            var subject = ExtractEmailSubject(text);
//            var date = ExtractDate(text);

//            return $"Email_{sender}_{subject}_{date}";
//        }

//        #endregion

//        #region Helper Extraction Methods

//        private string ExtractVendorName(string text)
//        {
//            // Look for company indicators
//            var companyPatterns = new[]
//            {
//                @"(?:From|Bill To|Vendor):\s*([A-Za-z\s&.,]+?)(?:\n|$)",
//                @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)\s+(?:Inc|LLC|Corp|Ltd|Company)",
//                @"^([A-Z][A-Za-z\s&.,]{2,30}?)(?:\n|\r|$)"
//            };

//            foreach (var pattern in companyPatterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.Multiline);
//                if (match.Success)
//                {
//                    return CleanCompanyName(match.Groups[1].Value.Trim());
//                }
//            }

//            return "Vendor";
//        }

//        private string ExtractDate(string text)
//        {
//            var datePatterns = new[]
//            {
//                @"(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})",
//                @"(\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2})",
//                @"((?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2},?\s+\d{4})"
//            };

//            foreach (var pattern in datePatterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
//                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
//                {
//                    return date.ToString("yyyy-MM-dd");
//                }
//            }

//            return DateTime.Now.ToString("yyyy-MM-dd");
//        }

//        private string ExtractAmount(string text)
//        {
//            var amountPatterns = new[]
//            {
//                @"\$\s*(\d+(?:,\d{3})*(?:\.\d{2})?)",
//                @"Total[:\s]+\$?\s*(\d+(?:,\d{3})*(?:\.\d{2})?)",
//                @"Amount[:\s]+\$?\s*(\d+(?:,\d{3})*(?:\.\d{2})?)"
//            };

//            foreach (var pattern in amountPatterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
//                if (match.Success)
//                {
//                    return match.Groups[1].Value.Replace(",", "");
//                }
//            }

//            return "";
//        }

//        private string ExtractPersonName(string text)
//        {
//            var namePatterns = new[]
//            {
//                @"(?:Name|To|For):\s*([A-Z][a-z]+\s+[A-Z][a-z]+)",
//                @"^([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\n|,)"
//            };

//            foreach (var pattern in namePatterns)
//            {
//                var match = Regex.Match(text, pattern, RegexOptions.Multiline);
//                if (match.Success)
//                {
//                    return match.Groups[1].Value.Trim();
//                }
//            }

//            return "Person";
//        }

//        #endregion

//        #region Text Processing

//        private string CleanExtractedText(string text)
//        {
//            if (string.IsNullOrWhiteSpace(text)) return "";

//            // Remove excessive whitespace and normalize
//            text = Regex.Replace(text, @"\s+", " ");
//            text = text.Trim();

//            // Remove OCR artifacts
//            text = Regex.Replace(text, @"[^\w\s\-.,;:()$%@#]", "");

//            return text;
//        }

//        private string GetFirstLineClean(string text)
//        {
//            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

//            foreach (var line in lines)
//            {
//                var cleanLine = line.Trim();
//                if (cleanLine.Length > 3 && IsValidFilenameContent(cleanLine))
//                {
//                    return TruncateAndClean(cleanLine, 50);
//                }
//            }

//            return "Document";
//        }

//        private bool IsValidFilenameContent(string text)
//        {
//            // Avoid lines that are just numbers, dates, or common headers
//            if (Regex.IsMatch(text, @"^\d+$")) return false;
//            if (Regex.IsMatch(text, @"^Page\s+\d+", RegexOptions.IgnoreCase)) return false;
//            if (Regex.IsMatch(text, @"^\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}$")) return false;

//            return true;
//        }

//        private string TruncateAndClean(string text, int maxLength)
//        {
//            if (text.Length <= maxLength) return text;

//            // Try to break at word boundary
//            var truncated = text.Substring(0, maxLength);
//            var lastSpace = truncated.LastIndexOf(' ');

//            if (lastSpace > maxLength / 2)
//            {
//                return truncated.Substring(0, lastSpace);
//            }

//            return truncated;
//        }

//        private string CleanCompanyName(string name)
//        {
//            // Remove common suffixes and clean
//            name = Regex.Replace(name, @"\b(?:Inc|LLC|Corp|Ltd|Company|Co)\b\.?", "", RegexOptions.IgnoreCase);
//            name = Regex.Replace(name, @"[^\w\s]", "");
//            name = Regex.Replace(name, @"\s+", "_");

//            return name.Trim('_');
//        }

//        #endregion

//        #region File Operations

//        public string RenameFile(string originalPath, string newFileName, ConflictResolution resolution = ConflictResolution.AddSuffix)
//        {
//            var directory = Path.GetDirectoryName(originalPath) ?? "";
//            var extension = Path.GetExtension(originalPath);
//            var sanitizedName = SanitizeFileName(newFileName);
//            var newPath = Path.Combine(directory, sanitizedName + extension);

//            if (File.Exists(newPath))
//            {
//                newPath = resolution switch
//                {
//                    ConflictResolution.AddSuffix => GenerateUniqueFileName(directory, sanitizedName, extension),
//                    ConflictResolution.Skip => originalPath,
//                    ConflictResolution.Overwrite => newPath,
//                    ConflictResolution.AddTimestamp => AddTimestampToFileName(directory, sanitizedName, extension),
//                    _ => throw new ArgumentException("Unsupported conflict resolution")
//                };
//            }

//            if (newPath != originalPath)
//            {
//                File.Move(originalPath, newPath);
//            }

//            return newPath;
//        }

//        private string SanitizeFileName(string fileName)
//        {
//            if (string.IsNullOrWhiteSpace(fileName)) return "Document";

//            var invalidChars = Path.GetInvalidFileNameChars();
//            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

//            // Remove stop words for cleaner names
//            var words = sanitized.Split('_', StringSplitOptions.RemoveEmptyEntries)
//                .Where(w => !_stopWords.Contains(w.ToLower()) && w.Length > 1)
//                .Take(8); // Limit to 8 meaningful words

//            sanitized = string.Join("_", words);

//            return string.IsNullOrWhiteSpace(sanitized) ? "Document" :
//                   sanitized.Substring(0, Math.Min(sanitized.Length, 100));
//        }

//        private string GenerateUniqueFileName(string directory, string baseName, string extension)
//        {
//            var counter = 1;
//            string newPath;
//            do
//            {
//                var fileName = $"{baseName}_{counter:D3}{extension}";
//                newPath = Path.Combine(directory, fileName);
//                counter++;
//            } while (File.Exists(newPath) && counter < 1000);

//            return newPath;
//        }

//        private string AddTimestampToFileName(string directory, string baseName, string extension)
//        {
//            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
//            var fileName = $"{baseName}_{timestamp}{extension}";
//            return Path.Combine(directory, fileName);
//        }

//        private string GenerateFallbackName()
//        {
//            return $"Document_{DateTime.Now:yyyyMMdd_HHmmss}";
//        }

//        #endregion
//    }
//}


using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.Renaming
{
    public class RenamingService : IRenamingService
    {
        public string GenerateNewName(string extractedText, RenamingRule rule)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
                return GenerateFallbackName();

            var cleanedText = CleanExtractedText(extractedText);

            return rule.Type switch
            {
                RuleType.FirstLine => GetFirstLineClean(cleanedText),
                RuleType.Regex => ApplyRegexRule(cleanedText, rule.Pattern, rule.Replacement),
                RuleType.Template => ApplyTemplate(cleanedText, rule.Template),
                RuleType.DateBased => GenerateDateBasedName(cleanedText),
                _ => GetFirstLineClean(cleanedText)
            };
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

        private string CleanExtractedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Remove excessive whitespace and normalize
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            // Remove OCR artifacts
            text = Regex.Replace(text, @"[^\w\s\-.,;:()$%@#]", "");

            return text;
        }

        private string GetFirstLineClean(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (cleanLine.Length > 3 && IsValidFilenameContent(cleanLine))
                {
                    return TruncateAndClean(cleanLine, 50);
                }
            }

            return "Document";
        }

        private string ApplyRegexRule(string text, string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(replacement))
                return GetFirstLineClean(text);

            try
            {
                var result = Regex.Replace(text, pattern, replacement);
                return SanitizeFileName(result);
            }
            catch
            {
                return GetFirstLineClean(text);
            }
        }

        private string ApplyTemplate(string text, string template)
        {
            if (string.IsNullOrEmpty(template))
                return GetFirstLineClean(text);

            // Simple template replacement
            var result = template
                .Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{time}", DateTime.Now.ToString("HH-mm-ss"))
                .Replace("{text}", text.Split('\n').FirstOrDefault()?.Trim() ?? "");

            return SanitizeFileName(result);
        }

        private string GenerateDateBasedName(string text)
        {
            var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            var firstWords = string.Join("_", text.Split(' ').Take(3));
            return SanitizeFileName($"{dateStr}_{firstWords}");
        }

        private bool IsValidFilenameContent(string text)
        {
            // Avoid lines that are just numbers, dates, or common headers
            if (Regex.IsMatch(text, @"^\d+$")) return false;
            if (Regex.IsMatch(text, @"^Page\s+\d+", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(text, @"^\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}$")) return false;

            return true;
        }

        private string TruncateAndClean(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;

            // Try to break at word boundary
            var truncated = text.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');

            if (lastSpace > maxLength / 2)
            {
                return truncated.Substring(0, lastSpace);
            }

            return truncated;
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
