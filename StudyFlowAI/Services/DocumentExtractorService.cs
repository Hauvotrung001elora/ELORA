using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace StudyFlowAI.Services;

public record DocumentExtractionResult(bool Success, string Text, string? ErrorMessage = null);

public class DocumentExtractorService
{
    public static readonly string[] AllowedExtensions = [".pdf", ".docx", ".txt"];
    public const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly ILogger<DocumentExtractorService> _logger;

    public DocumentExtractorService(ILogger<DocumentExtractorService> logger)
    {
        _logger = logger;
    }

    public bool IsSupportedExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(ext);
    }

    public async Task<DocumentExtractionResult> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new DocumentExtractionResult(false, string.Empty, "Không tìm thấy tệp tài liệu để trích xuất.");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            string rawText = extension switch
            {
                ".pdf" => await Task.Run(() => ExtractFromPdf(filePath), cancellationToken),
                ".docx" => await Task.Run(() => ExtractFromDocx(filePath), cancellationToken),
                ".txt" => await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken),
                _ => throw new NotSupportedException($"Định dạng tệp '{extension}' chưa được hỗ trợ.")
            };

            var normalized = NormalizeText(rawText);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new DocumentExtractionResult(
                    false,
                    string.Empty,
                    "Không trích xuất được nội dung văn bản từ tệp (tệp có thể rỗng hoặc chỉ chứa hình ảnh scan).");
            }

            return new DocumentExtractionResult(true, normalized);
        }
        catch (NotSupportedException ex)
        {
            return new DocumentExtractionResult(false, string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trích xuất văn bản từ tệp {FilePath}", filePath);
            return new DocumentExtractionResult(
                false,
                string.Empty,
                "Không thể đọc nội dung tệp. Vui lòng kiểm tra tệp có bị hỏng hoặc đặt mật khẩu không.");
        }
    }

    private static string ExtractFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string ExtractFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var wordDoc = WordprocessingDocument.Open(filePath, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        return sb.ToString();
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var unifiedNewLines = input.Replace("\r\n", "\n").Replace("\r", "\n");
        var collapsedBlankLines = Regex.Replace(unifiedNewLines, @"\n{3,}", "\n\n");
        return collapsedBlankLines.Trim();
    }
}
