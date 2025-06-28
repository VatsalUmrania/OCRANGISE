using System;
using System.IO;
using Tesseract;
using OCRANGISE.Core.Models;

namespace OCRANGISE.Core.OCR
{
    public class TesseractOcrProcessor : IOcrProcessor
    {
        private readonly string _tessdataPath;
        private readonly string _language;

        public TesseractOcrProcessor(string tessdataPath = null, string language = "eng")
        {
            // Fix 1: Use absolute path instead of relative path
            _tessdataPath = tessdataPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _language = language;

            // Fix 2: Add validation to check if tessdata exists
            if (!Directory.Exists(_tessdataPath))
            {
                throw new DirectoryNotFoundException($"Tessdata directory not found: {_tessdataPath}");
            }

            var langFile = Path.Combine(_tessdataPath, $"{_language}.traineddata");
            if (!File.Exists(langFile))
            {
                throw new FileNotFoundException($"Language file not found: {langFile}. Please download eng.traineddata from https://github.com/tesseract-ocr/tessdata");
            }
        }

        public string ExtractText(string filePath)
        {
            try
            {
                // Fix 3: Add debug output to verify paths
                Console.WriteLine($"Using tessdata path: {_tessdataPath}");
                Console.WriteLine($"Language file exists: {File.Exists(Path.Combine(_tessdataPath, $"{_language}.traineddata"))}");

                using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);

                if (GetFileType(filePath) == FileType.PDF)
                {
                    throw new NotSupportedException("PDF OCR requires additional PDF-to-image conversion");
                }

                using var img = Pix.LoadFromFile(filePath);
                using var page = engine.Process(img);
                return page.GetText().Trim();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"OCR failed for {filePath}: {ex.Message}", ex);
            }
        }

        public bool IsSupported(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension is ".jpg" or ".jpeg" or ".png" or ".tiff" or ".bmp" or ".pdf";
        }

        private FileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".pdf" => FileType.PDF,
                ".jpg" or ".jpeg" or ".png" or ".tiff" or ".bmp" => FileType.Image,
                _ => FileType.Unknown
            };
        }
    }
}
