using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyFlowAI.Services;

public class GeminiApiException : Exception
{
    public GeminiApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public class GeneratedCauHoiDto
{
    [JsonPropertyName("NoiDung")]
    public string NoiDung { get; set; } = string.Empty;

    [JsonPropertyName("DapAnA")]
    public string DapAnA { get; set; } = string.Empty;

    [JsonPropertyName("DapAnB")]
    public string DapAnB { get; set; } = string.Empty;

    [JsonPropertyName("DapAnC")]
    public string DapAnC { get; set; } = string.Empty;

    [JsonPropertyName("DapAnD")]
    public string DapAnD { get; set; } = string.Empty;

    [JsonPropertyName("DapAnDung")]
    public string DapAnDung { get; set; } = "A";

    [JsonPropertyName("GiaiThichAI")]
    public string GiaiThichAI { get; set; } = string.Empty;
}

public class PlannerDocumentInput
{
    public int MaTaiLieu { get; set; }
    public string TieuDe { get; set; } = string.Empty;
}

public class PlannerSubjectInput
{
    public int MaMonHoc { get; set; }
    public string TenMonHoc { get; set; } = string.Empty;
    public DateTime? NgayThi { get; set; }
    public double? DiemMucTieu { get; set; }
    public double? DiemTrungBinhHienTai { get; set; }
    public int SoLuotLamBai { get; set; }
    public int DoKho { get; set; }
    public List<PlannerDocumentInput> TaiLieus { get; set; } = new();
}

public class ScanThoiKhoaBieuResultDto
{
    [JsonPropertyName("DanhSachMonHoc")]
    public List<string> DanhSachMonHoc { get; set; } = new();

    [JsonPropertyName("LichBanTrenTruong")]
    public string LichBanTrenTruong { get; set; } = string.Empty;
}

public class PlannerSessionSummaryInput
{
    public int MaMonHoc { get; set; }
    public string TenMonHoc { get; set; } = string.Empty;
    public int? MaTaiLieu { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public DateTime NgayHoc { get; set; }
    public TimeSpan GioBatDau { get; set; }
    public int ThoiLuongPhut { get; set; }
}

public class PlannerWeakQuizInput
{
    public int MaMonHoc { get; set; }
    public string TenMonHoc { get; set; } = string.Empty;
    public int MaTaiLieu { get; set; }
    public string TenTaiLieu { get; set; } = string.Empty;
    public string TenDeThi { get; set; } = string.Empty;
    public double DiemSo { get; set; }
    public DateTime NgayLam { get; set; }
}

public class GeneratedStudySessionDto
{
    [JsonPropertyName("MaMonHoc")]
    public int MaMonHoc { get; set; }

    [JsonPropertyName("MaTaiLieu")]
    public int? MaTaiLieu { get; set; }

    [JsonPropertyName("TieuDe")]
    public string TieuDe { get; set; } = string.Empty;

    [JsonPropertyName("NgayHoc")]
    public string NgayHoc { get; set; } = string.Empty;

    [JsonPropertyName("GioBatDau")]
    public string GioBatDau { get; set; } = string.Empty;

    [JsonPropertyName("ThoiLuongPhut")]
    public int ThoiLuongPhut { get; set; } = 60;
}

public class GeminiService
{
    private const string DefaultModel = "gemini-3.5-flash-lite";
    private const string GeminiApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const int MaxInputCharsForSummary = 45_000;
    private const int MaxInputCharsForQuiz = 40_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    private string GetApiKey() => _configuration["Gemini:ApiKey"]?.Trim() ?? string.Empty;

    private string GetModel() => NormalizeModelName(_configuration["Gemini:Model"]);

    private static string NormalizeModelName(string? rawModel)
    {
        if (string.IsNullOrWhiteSpace(rawModel))
        {
            return DefaultModel;
        }

        var normalized = rawModel.Trim().Trim('/');
        while (normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["models/".Length..].TrimStart('/');
        }

        return string.IsNullOrWhiteSpace(normalized) ? DefaultModel : normalized;
    }

    public async Task<string> TomTatTaiLieuAsync(string noiDung, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            throw new GeminiApiException("Tài liệu chưa có nội dung văn bản để tóm tắt.");
        }

        var trimmedContent = TruncateSmart(noiDung, MaxInputCharsForSummary);
        var prompt = $"""
            Bạn là trợ lý học tập AI chuyên nghiệp. Hãy tóm tắt nội dung tài liệu học tập dưới đây bằng Tiếng Việt rõ ràng, mạch lạc, định dạng Markdown sạch sẽ (sử dụng tiêu đề `##`, danh sách gạch đầu dòng, in đậm thuật ngữ quan trọng).

            Cấu trúc tóm tắt gồm:
            1. ## Tổng quan ngắn gọn
            2. ## Các ý chính & Khái niệm trọng tâm
            3. ## Công thức / Quy tắc / Ví dụ cần nhớ (nếu có trong tài liệu)
            4. ## Gợi ý trọng tâm ôn thi

            Chỉ dựa vào thông tin có trong tài liệu, không bịa thêm dữ kiện sai lệch.

            --- NỘI DUNG TÀI LIỆU ---
            {trimmedContent}
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.4
            }
        };

        var resultText = await SendGenerateContentAsync(payload, cancellationToken);
        return resultText.Trim();
    }

    public async Task<List<GeneratedCauHoiDto>> TaoDeThiTracNghiemAsync(
        string noiDung,
        int soCau,
        string doKho,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            throw new GeminiApiException("Tài liệu chưa có nội dung văn bản để tạo đề trắc nghiệm.");
        }

        soCau = Math.Clamp(soCau, 1, 30);
        var moTaDoKho = doKho switch
        {
            "De" => "Dễ (tập trung vào nhận biết khái niệm, định nghĩa cơ bản)",
            "Kho" => "Khó (tập trung vào phân tích sâu, so sánh, tình huống vận dụng cao, các phương án nhiễu hợp lý)",
            _ => "Trung bình (kết hợp thông hiểu và vận dụng kiến thức)"
        };

        var trimmedContent = TruncateSmart(noiDung, MaxInputCharsForQuiz);
        var prompt = $"""
            Bạn là giảng viên ra đề thi trắc nghiệm. Dựa hoàn toàn vào nội dung tài liệu dưới đây, hãy tạo đúng {soCau} câu hỏi trắc nghiệm 4 lựa chọn (A, B, C, D) bằng Tiếng Việt.
            Mức độ khó yêu cầu: {moTaDoKho}.

            Yêu cầu bắt buộc:
            - Mỗi câu hỏi có duy nhất 1 đáp án đúng (`DapAnDung` chỉ là một trong các ký tự: "A", "B", "C", "D").
            - Các trường `DapAnA`, `DapAnB`, `DapAnC`, `DapAnD` chỉ ghi nội dung đáp án, không cần lặp lại tiền tố "A.", "B." ở đầu.
            - `GiaiThichAI` giải thích ngắn gọn, rõ ràng vì sao đáp án đúng và trích dẫn ý liên quan trong tài liệu.
            - Trải đều kiến thức trong tài liệu, không lặp câu hỏi.

            --- NỘI DUNG TÀI LIỆU ---
            {trimmedContent}
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.5,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            NoiDung = new { type = "STRING" },
                            DapAnA = new { type = "STRING" },
                            DapAnB = new { type = "STRING" },
                            DapAnC = new { type = "STRING" },
                            DapAnD = new { type = "STRING" },
                            DapAnDung = new { type = "STRING", @enum = new[] { "A", "B", "C", "D" } },
                            GiaiThichAI = new { type = "STRING" }
                        },
                        required = new[]
                        {
                            "NoiDung", "DapAnA", "DapAnB", "DapAnC", "DapAnD", "DapAnDung", "GiaiThichAI"
                        }
                    }
                }
            }
        };

        var jsonText = await SendGenerateContentAsync(payload, cancellationToken);
        jsonText = CleanJsonFence(jsonText);

        List<GeneratedCauHoiDto>? questions;
        try
        {
            questions = JsonSerializer.Deserialize<List<GeneratedCauHoiDto>>(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không thể parse JSON câu hỏi từ Gemini: {Json}", jsonText);
            throw new GeminiApiException("AI trả về dữ liệu đề thi không đúng định dạng. Vui lòng thử lại.", ex);
        }

        if (questions is null || questions.Count == 0)
        {
            throw new GeminiApiException("AI không tạo được câu hỏi nào từ tài liệu này. Vui lòng thử lại.");
        }

        foreach (var q in questions)
        {
            q.NoiDung = q.NoiDung?.Trim() ?? string.Empty;
            q.DapAnA = StripOptionPrefix(q.DapAnA);
            q.DapAnB = StripOptionPrefix(q.DapAnB);
            q.DapAnC = StripOptionPrefix(q.DapAnC);
            q.DapAnD = StripOptionPrefix(q.DapAnD);
            q.DapAnDung = NormalizeCorrectAnswer(q.DapAnDung);
            q.GiaiThichAI = q.GiaiThichAI?.Trim() ?? string.Empty;
        }

        return questions
            .Where(q => !string.IsNullOrWhiteSpace(q.NoiDung))
            .ToList();
    }

    public async Task<string> NhanXetKetQuaBaiThiAsync(
        string tenDe,
        double diemSo,
        List<string> danhSachCauSai,
        CancellationToken cancellationToken = default)
    {
        var saiSummary = danhSachCauSai.Count == 0
            ? "Sinh viên trả lời đúng tất cả các câu hỏi, không có câu sai."
            : string.Join("\n", danhSachCauSai.Take(15).Select((c, i) => $"{i + 1}. {c}"));

        var prompt = $"""
            Bạn là cố vấn học tập AI. Hãy viết một đoạn nhận xét ngắn gọn (khoảng 4-6 câu, Tiếng Việt thân thiện và khích lệ) cho sinh viên vừa hoàn thành bài thi trắc nghiệm.

            - Tên đề thi: {tenDe}
            - Điểm số đạt được: {diemSo:0.##}/10
            - Số câu làm sai: {danhSachCauSai.Count}
            - Nội dung các câu làm sai:
            {saiSummary}

            Hãy chỉ ra ngắn gọn lỗ hổng kiến thức chính (nếu có) và đề xuất 2-3 gạch đầu dòng cụ thể nên ôn tập lại. Không dùng tiêu đề lớn, viết gọn gàng dễ đọc.
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.5
            }
        };

        var resultText = await SendGenerateContentAsync(payload, cancellationToken);
        return resultText.Trim();
    }

    public async Task<ScanThoiKhoaBieuResultDto> ScanThoiKhoaBieuAsync(
        byte[] imageBytes,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new GeminiApiException("Ảnh thời khóa biểu trống hoặc không hợp lệ.");
        }

        var normalizedMime = mimeType?.Trim().ToLowerInvariant() switch
        {
            "image/jpg" or "image/jpeg" => "image/jpeg",
            "image/png" => "image/png",
            "image/webp" => "image/webp",
            _ => throw new GeminiApiException("Định dạng ảnh không hỗ trợ. Chỉ chấp nhận JPG, PNG hoặc WEBP.")
        };

        var base64Image = Convert.ToBase64String(imageBytes);
        var prompt = """
            Bạn là trợ lý học tập AI chuyên đọc ảnh Thời khóa biểu (TKB) của sinh viên Việt Nam.
            Hãy quan sát thật kỹ toàn bộ bảng/lịch trong ảnh và trích xuất chính xác 2 thông tin sau dưới dạng JSON:

            1. `DanhSachMonHoc`: Mảng chứa TÊN CÁC MÔN HỌC DUY NHẤT xuất hiện trong thời khóa biểu.
               - Chuẩn hóa tên môn học sạch sẽ, đúng chính tả Tiếng Việt có dấu.
               - Bỏ các thông tin phụ như mã học phần, nhóm tổ, phòng học, giảng viên, tiết học nếu không phải một phần tên môn.
               - Tuyệt đối không lặp lại cùng một môn dù môn đó học nhiều buổi/tuần (cả lý thuyết & thực hành cùng một môn chỉ ghi 1 tên duy nhất).

            2. `LichBanTrenTruong`: Chuỗi Tiếng Việt tóm tắt ngắn gọn các khung giờ bận cố định trên trường theo từng thứ trong tuần đọc được từ ảnh.
               - Định dạng rõ ràng, dễ hiểu để dùng làm lịch bận khi sắp lịch tự học. Ví dụ:
                 "Thứ 2: 07:30-11:30 (Cơ sở dữ liệu); Thứ 3: 13:00-16:30 (Mạng máy tính); Thứ 5: 07:30-10:30 (Trí tuệ nhân tạo)"
               - Nếu ảnh chỉ ghi theo tiết (VD: Tiết 1-3, Tiết 7-9) mà không ghi giờ, hãy ghi rõ cả tiết lẫn giờ ước lượng (VD: "07:00-09:30 / Tiết 1-3").
               - Nếu không đọc được khung giờ cụ thể từ ảnh, trả về chuỗi rỗng "".

            Chỉ trích xuất thông tin thực sự có trong ảnh, không bịa thêm môn học không tồn tại.
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inlineData = new
                            {
                                mimeType = normalizedMime,
                                data = base64Image
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        DanhSachMonHoc = new
                        {
                            type = "ARRAY",
                            items = new { type = "STRING" }
                        },
                        LichBanTrenTruong = new { type = "STRING" }
                    },
                    required = new[] { "DanhSachMonHoc", "LichBanTrenTruong" }
                }
            }
        };

        var jsonText = await SendGenerateContentAsync(payload, cancellationToken);
        jsonText = CleanJsonFence(jsonText);

        ScanThoiKhoaBieuResultDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ScanThoiKhoaBieuResultDto>(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không thể parse JSON kết quả scan TKB từ Gemini: {Json}", jsonText);
            throw new GeminiApiException("AI trả về dữ liệu thời khóa biểu không đúng định dạng. Vui lòng thử lại với ảnh rõ nét hơn.", ex);
        }

        if (parsed is null)
        {
            throw new GeminiApiException("AI không đọc được thông tin từ ảnh thời khóa biểu.");
        }

        // Lọc bỏ môn rỗng và khử trùng lặp không phân biệt hoa/thường
        var uniqueSubjects = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawName in parsed.DanhSachMonHoc ?? new List<string>())
        {
            var cleaned = string.Join(' ', (rawName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            if (cleaned.Length > 200)
            {
                cleaned = cleaned[..200];
            }

            if (seen.Add(cleaned))
            {
                uniqueSubjects.Add(cleaned);
            }
        }

        return new ScanThoiKhoaBieuResultDto
        {
            DanhSachMonHoc = uniqueSubjects,
            LichBanTrenTruong = (parsed.LichBanTrenTruong ?? string.Empty).Trim()
        };
    }

    public async Task<List<GeneratedStudySessionDto>> TaoLichHocTapAsync(
        List<PlannerSubjectInput> danhSachMonHoc,
        DateTime ngayBatDau,
        int soNgay,
        string khungGioRanh,
        CancellationToken cancellationToken = default)
    {
        if (danhSachMonHoc.Count == 0)
        {
            throw new GeminiApiException("Vui lòng chọn ít nhất một môn học để lập lịch.");
        }

        soNgay = Math.Clamp(soNgay, 1, 30);
        var ngayKetThuc = ngayBatDau.Date.AddDays(soNgay - 1);
        var calendarDayMapping = ScheduleConstraintHelper.BuildCalendarDayMapping(ngayBatDau.Date, soNgay);
        var subjectsJson = JsonSerializer.Serialize(danhSachMonHoc);
        var ranhText = string.IsNullOrWhiteSpace(khungGioRanh)
            ? "Thứ 2 - Thứ 6: 19:30 - 21:30; Thứ 7 - Chủ nhật: 09:00 - 11:30 và 15:00 - 17:00"
            : khungGioRanh.Trim();

        var prompt = $"""
            Bạn là trợ lý lập kế hoạch học tập AI. Hãy thiết kế một lịch ôn tập cá nhân hóa hợp lý bằng Tiếng Việt từ ngày {ngayBatDau:yyyy-MM-dd} đến hết ngày {ngayKetThuc:yyyy-MM-dd} ({soNgay} ngày).

            BẢNG ÁNH XẠ NGÀY -> THỨ CHÍNH XÁC (BẮT BUỘC TRA CỨU TỪ BẢNG NÀY, KHÔNG TỰ TÍNH THỨ):
            {calendarDayMapping}

            Thông tin đầu vào:
            - Mô tả lịch bận (TKB trên trường / việc cá nhân) và khung giờ rảnh của sinh viên:
              {ranhText}
            - Danh sách môn học, mục tiêu điểm, điểm trung bình hiện tại & tài liệu (JSON):
              {subjectsJson}

            Quy tắc bắt buộc:
            1. CHỈ ĐƯỢC chọn `NgayHoc` nằm trong BẢNG ÁNH XẠ NGÀY -> THỨ ở trên (định dạng `yyyy-MM-dd`).
            2. TRƯỚC KHI chọn `GioBatDau` và `ThoiLuongPhut` cho một `NgayHoc`, BẮT BUỘC tra đúng Thứ của ngày đó từ bảng trên:
               - Tính giờ kết thúc `GioKetThuc = GioBatDau + ThoiLuongPhut`.
               - TUYỆT ĐỐI KHÔNG để khoảng `[GioBatDau, GioKetThuc]` giao nhau với bất kỳ khung giờ BẬN hoặc lịch học trên trường nào của Thứ đó.
               - CHỈ ĐƯỢC xếp buổi học nằm trọn trong khung giờ RẢNH của Thứ đó (nếu người dùng có chỉ định khung giờ rảnh) hoặc khung giờ trống hợp lý ngoài giờ bận.
            3. Ưu tiên phân bổ NHIỀU buổi học hơn và sớm hơn cho các môn:
               - Có `NgayThi` sắp đến gần.
               - Có `DoKho` cao hơn (1=Dễ, 2=Trung bình, 3=Khó).
               - Có `DiemMucTieu` cao và đặc biệt là các môn có `DiemTrungBinhHienTai` thấp hơn / còn xa `DiemMucTieu` (hoặc chưa có lượt làm bài nhưng đặt `DiemMucTieu` cao).
            4. `MaMonHoc` BẮT BUỘC phải lấy đúng từ danh sách JSON đầu vào.
            5. `MaTaiLieu` nếu có tài liệu phù hợp thuộc chính môn đó thì điền đúng `MaTaiLieu`, nếu không có tài liệu thì để `null`.
            6. `GioBatDau` đúng định dạng `HH:mm` (24h, ví dụ "19:30"), không chồng chéo giờ nhau giữa các buổi trong cùng 1 ngày.
            7. `ThoiLuongPhut` từ 30 đến 120 phút (khuyến nghị 45, 60 hoặc 90 phút).
            8. `TieuDe` ngắn gọn, rõ mục tiêu buổi học (VD: "Ôn chương 1 & làm bài tập", "Luyện đề nâng điểm lên mục tiêu 8.5").
            """;

        return await RequestStudySessionsAsync(prompt, cancellationToken);
    }

    public async Task<List<GeneratedStudySessionDto>> DieuChinhLichHocAsync(
        List<PlannerSubjectInput> danhSachMonHoc,
        List<PlannerSessionSummaryInput> buoiBoLo,
        List<PlannerSessionSummaryInput> buoiChuaLam,
        List<PlannerWeakQuizInput> baiThiDiemThap,
        DateTime ngayBatDau,
        string khungGioRanh,
        CancellationToken cancellationToken = default)
    {
        if (danhSachMonHoc.Count == 0)
        {
            throw new GeminiApiException("Chưa có môn học nào để điều chỉnh lịch học.");
        }

        const int adjustDays = 14;
        var ngayKetThuc = ngayBatDau.Date.AddDays(adjustDays - 1);
        var calendarDayMapping = ScheduleConstraintHelper.BuildCalendarDayMapping(ngayBatDau.Date, adjustDays);
        var subjectsJson = JsonSerializer.Serialize(danhSachMonHoc);
        var missedJson = JsonSerializer.Serialize(buoiBoLo);
        var upcomingJson = JsonSerializer.Serialize(buoiChuaLam);
        var weakQuizJson = JsonSerializer.Serialize(baiThiDiemThap);
        var ranhText = string.IsNullOrWhiteSpace(khungGioRanh)
            ? "Các buổi tối 19:30 - 21:30 và cuối tuần 09:00 - 11:30"
            : khungGioRanh.Trim();

        var prompt = $"""
            Bạn là cố vấn học tập AI. Sinh viên có một số buổi học bị bỏ lỡ và/hoặc kết quả bài thi gần đây điểm thấp (< 6.5/10). Hãy lập danh sách các buổi ôn bù và củng cố kiến thức ưu tiên trong khoảng từ {ngayBatDau:yyyy-MM-dd} đến {ngayKetThuc:yyyy-MM-dd}.

            BẢNG ÁNH XẠ NGÀY -> THỨ CHÍNH XÁC (BẮT BUỘC TRA CỨU TỪ BẢNG NÀY, KHÔNG TỰ TÍNH THỨ):
            {calendarDayMapping}

            Dữ liệu hiện tại:
            - Mô tả lịch bận (TKB / cá nhân) & khung giờ rảnh ưu tiên để học bù:
              {ranhText}
            - Danh sách môn học, mục tiêu điểm, điểm TB hiện tại & tài liệu của sinh viên:
            {subjectsJson}
            - Các buổi học đã BỎ LỠ (cần học bù):
            {missedJson}
            - Các buổi học CHƯA LÀM sắp tới (tránh xếp trùng ngày & giờ):
            {upcomingJson}
            - Các bài thi gần đây điểm thấp (< 6.5/10, cần ôn tập lại tài liệu tương ứng):
            {weakQuizJson}

            Quy tắc bắt buộc:
            1. CHỈ ĐƯỢC chọn `NgayHoc` nằm trong BẢNG ÁNH XẠ NGÀY -> THỨ ở trên (định dạng `yyyy-MM-dd`).
            2. TRƯỚC KHI chọn `GioBatDau` và `ThoiLuongPhut` cho một `NgayHoc`, BẮT BUỘC tra đúng Thứ của ngày đó từ bảng trên:
               - Khoảng `[GioBatDau, GioBatDau + ThoiLuongPhut]` TUYỆT ĐỐI KHÔNG giao nhau với bất kỳ khung giờ BẬN / giờ học trên trường nào của Thứ đó và không trùng các buổi CHƯA LÀM hiện có.
               - CHỈ ĐƯỢC xếp nằm trọn trong khung giờ RẢNH của Thứ đó.
            3. Tạo từ 3 đến 10 buổi học bù/củng cố thiết thực nhất, ưu tiên các môn/tài liệu có điểm thi thấp, buổi bị bỏ lỡ, môn có `NgayThi` sắp tới và `DiemMucTieu` cao.
            4. `MaMonHoc` BẮT BUỘC khớp với danh sách môn học ở trên.
            5. `MaTaiLieu` phải thuộc đúng `MaMonHoc` đó hoặc để `null`.
            6. `GioBatDau` định dạng `HH:mm` (24h); `ThoiLuongPhut` từ 30 đến 120 phút.
            7. `TieuDe` ghi rõ nội dung ôn bù hoặc củng cố (VD: "[Ôn bù] ...", "[Củng cố điểm yếu] ...").
            """;

        return await RequestStudySessionsAsync(prompt, cancellationToken);
    }

    private async Task<List<GeneratedStudySessionDto>> RequestStudySessionsAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            MaMonHoc = new { type = "INTEGER" },
                            MaTaiLieu = new { type = "INTEGER", nullable = true },
                            TieuDe = new { type = "STRING" },
                            NgayHoc = new { type = "STRING" },
                            GioBatDau = new { type = "STRING" },
                            ThoiLuongPhut = new { type = "INTEGER" }
                        },
                        required = new[]
                        {
                            "MaMonHoc", "TieuDe", "NgayHoc", "GioBatDau", "ThoiLuongPhut"
                        }
                    }
                }
            }
        };

        var jsonText = await SendGenerateContentAsync(payload, cancellationToken);
        jsonText = CleanJsonFence(jsonText);

        List<GeneratedStudySessionDto>? sessions;
        try
        {
            sessions = JsonSerializer.Deserialize<List<GeneratedStudySessionDto>>(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không thể parse JSON lịch học từ Gemini: {Json}", jsonText);
            throw new GeminiApiException("AI trả về dữ liệu lịch học không đúng định dạng. Vui lòng thử lại.", ex);
        }

        if (sessions is null || sessions.Count == 0)
        {
            throw new GeminiApiException("AI không đề xuất được buổi học nào. Vui lòng thử điều chỉnh khung giờ rảnh và thử lại.");
        }

        return sessions;
    }

    private async Task<string> SendGenerateContentAsync(object payload, CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GeminiApiException(
                "Chưa cấu hình Gemini API Key. Vui lòng thiết lập 'Gemini:ApiKey' trong appsettings.json hoặc dotnet user-secrets.");
        }

        var model = GetModel();
        var jsonBody = JsonSerializer.Serialize(payload);

        var (statusCode, isSuccess, responseBody) =
            await PostGenerateContentRawAsync(model, apiKey, jsonBody, cancellationToken);

        // Nếu model cấu hình trả về 404, tự động tra cứu danh sách model khả dụng một lần và thử fallback sang model flash phù hợp
        if (statusCode == HttpStatusCode.NotFound)
        {
            var initialError = ExtractGoogleErrorMessage(responseBody);
            _logger.LogWarning(
                "Model Gemini '{Model}' trả về HTTP 404 ({ErrorMessage}). Đang kiểm tra danh sách model khả dụng...",
                model,
                initialError ?? "Not Found");

            var fallbackModel = await DiscoverFallbackFlashModelAsync(apiKey, model, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fallbackModel))
            {
                _logger.LogInformation(
                    "Tự động chuyển từ model '{OldModel}' sang model khả dụng '{FallbackModel}'.",
                    model,
                    fallbackModel);

                model = fallbackModel;
                (statusCode, isSuccess, responseBody) =
                    await PostGenerateContentRawAsync(model, apiKey, jsonBody, cancellationToken);
            }
        }

        if (!isSuccess)
        {
            var googleError = ExtractGoogleErrorMessage(responseBody);
            _logger.LogError(
                "Gemini API trả về lỗi HTTP {StatusCode} cho model '{Model}'. Chi tiết: {ErrorMessage} | Raw: {Body}",
                (int)statusCode,
                model,
                googleError ?? "(không có error.message)",
                responseBody);

            throw new GeminiApiException(BuildFriendlyErrorMessage(statusCode, model, googleError));
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                throw new GeminiApiException("Gemini AI không trả về nội dung phản hồi. Vui lòng thử lại.");
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                throw new GeminiApiException("Phản hồi từ Gemini AI trống. Vui lòng thử lại.");
            }

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElem))
                {
                    sb.Append(textElem.GetString());
                }
            }

            var combined = sb.ToString();
            if (string.IsNullOrWhiteSpace(combined))
            {
                throw new GeminiApiException("Gemini AI trả về nội dung rỗng. Vui lòng thử lại.");
            }

            return combined;
        }
        catch (GeminiApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân tích phản hồi Gemini API: {Body}", responseBody);
            throw new GeminiApiException("Không thể đọc phản hồi từ Gemini AI.", ex);
        }
    }

    private async Task<(HttpStatusCode StatusCode, bool IsSuccess, string Body)> PostGenerateContentRawAsync(
        string model,
        string apiKey,
        string jsonBody,
        CancellationToken cancellationToken)
    {
        var normalizedModel = NormalizeModelName(model);
        var endpoint = $"{GeminiApiBaseUrl}/{Uri.EscapeDataString(normalizedModel)}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.StatusCode, response.IsSuccessStatusCode, body);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Hết thời gian chờ khi gọi Gemini API (model: {Model}).", normalizedModel);
            throw new GeminiApiException("Kết nối tới Gemini AI quá thời gian chờ. Vui lòng thử lại.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi mạng khi gọi Gemini API (model: {Model}).", normalizedModel);
            throw new GeminiApiException("Không thể kết nối tới máy chủ Gemini AI. Vui lòng kiểm tra kết nối mạng.", ex);
        }
    }

    private async Task<string?> DiscoverFallbackFlashModelAsync(
        string apiKey,
        string failedModel,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GeminiApiBaseUrl);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Không thể lấy danh sách model Gemini (HTTP {StatusCode}): {Error}",
                    (int)response.StatusCode,
                    ExtractGoogleErrorMessage(body) ?? body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElem) ||
                modelsElem.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var supportedModels = new List<string>();
            foreach (var item in modelsElem.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameElem))
                {
                    continue;
                }

                var supportsGenerateContent = false;
                if (item.TryGetProperty("supportedGenerationMethods", out var methodsElem) &&
                    methodsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in methodsElem.EnumerateArray())
                    {
                        if (string.Equals(m.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsGenerateContent = true;
                            break;
                        }
                    }
                }

                if (!supportsGenerateContent)
                {
                    continue;
                }

                var modelId = NormalizeModelName(nameElem.GetString());
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    supportedModels.Add(modelId);
                }
            }

            if (supportedModels.Count == 0)
            {
                _logger.LogWarning("API Key hiện tại không có model nào hỗ trợ generateContent.");
                return null;
            }

            _logger.LogInformation(
                "Các model Gemini hỗ trợ generateContent khả dụng ({Count}): {Models}",
                supportedModels.Count,
                string.Join(", ", supportedModels.Take(20)));

            // Ưu tiên chọn model gemini flash (không phải bản image/tts/audio/robotics/embedding) và khác model vừa lỗi 404
            var candidates = supportedModels
                .Where(m =>
                    !string.Equals(m, failedModel, StringComparison.OrdinalIgnoreCase) &&
                    m.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase) &&
                    !m.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
                    !m.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                    !m.Contains("image", StringComparison.OrdinalIgnoreCase) &&
                    !m.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
                    !m.Contains("robotics", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var flashCandidates = candidates
                .Where(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(GetModelPriorityScore)
                .ThenByDescending(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (flashCandidates.Count > 0)
            {
                return flashCandidates[0];
            }

            return candidates
                .OrderByDescending(GetModelPriorityScore)
                .ThenByDescending(m => m, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Lỗi khi tra cứu danh sách model Gemini khả dụng.");
            return null;
        }
    }

    private static int GetModelPriorityScore(string modelName)
    {
        var score = 0;
        var lower = modelName.ToLowerInvariant();

        // Ưu tiên thế hệ mới hơn (4.x > 3.5 > 3.0 > 2.5 > 2.0 > 1.5)
        if (lower.Contains("gemini-4")) score += 600;
        else if (lower.Contains("gemini-3.5")) score += 500;
        else if (lower.Contains("gemini-3")) score += 450;
        else if (lower.Contains("gemini-2.5")) score += 400;
        else if (lower.Contains("gemini-2.0") || lower.Contains("gemini-2-")) score += 300;
        else if (lower.Contains("gemini-1.5")) score += 200;

        if (lower.Contains("flash-lite")) score += 40;
        else if (lower.Contains("flash")) score += 30;

        // Ưu tiên bản ổn định hơn preview/exp
        if (!lower.Contains("preview") && !lower.Contains("exp")) score += 20;

        return score;
    }

    private static string? ExtractGoogleErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var errorElem) &&
                errorElem.ValueKind == JsonValueKind.Object)
            {
                string? message = null;
                string? status = null;

                if (errorElem.TryGetProperty("message", out var msgElem))
                {
                    message = msgElem.GetString()?.Trim();
                }

                if (errorElem.TryGetProperty("status", out var statusElem))
                {
                    status = statusElem.GetString()?.Trim();
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return string.IsNullOrWhiteSpace(status)
                        ? message
                        : $"{status}: {message}";
                }
            }
        }
        catch (JsonException)
        {
            // Phản hồi không phải JSON chuẩn
        }

        return null;
    }

    private static string BuildFriendlyErrorMessage(HttpStatusCode statusCode, string model, string? googleError)
    {
        var detail = string.IsNullOrWhiteSpace(googleError)
            ? string.Empty
            : $" Chi tiết từ Google: {(googleError.Length > 260 ? googleError[..260] + "…" : googleError)}";

        return (int)statusCode switch
        {
            400 => $"Yêu cầu gửi tới Gemini AI không hợp lệ (model '{model}').{detail}",
            401 or 403 => $"Gemini API Key không hợp lệ hoặc không có quyền truy cập model '{model}'.{detail}",
            404 => $"Model Gemini '{model}' không tồn tại hoặc không còn hỗ trợ với API Key này. Vui lòng cập nhật 'Gemini:Model' trong appsettings.json.{detail}",
            429 => $"Đã vượt giới hạn tần suất / quota của Gemini API.{detail}",
            >= 500 => $"Máy chủ Gemini AI đang tạm thời gặp sự cố (HTTP {(int)statusCode}).{detail}",
            _ => $"Gemini AI trả về lỗi HTTP {(int)statusCode} (model '{model}').{detail}"
        };
    }

    private static string TruncateSmart(string content, int maxChars)
    {
        var trimmed = content.Trim();
        if (trimmed.Length <= maxChars)
        {
            return trimmed;
        }

        var headLength = (int)(maxChars * 0.72);
        var tailLength = maxChars - headLength;
        return $"{trimmed[..headLength]}\n\n[... phần nội dung giữa đã được rút gọn để đảm bảo giới hạn xử lý AI ...]\n\n{trimmed[^tailLength..]}";
    }

    private static string CleanJsonFence(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..];
            }

            if (text.EndsWith("```"))
            {
                text = text[..^3];
            }
        }

        return text.Trim();
    }

    private static string StripOptionPrefix(string? option)
    {
        if (string.IsNullOrWhiteSpace(option))
        {
            return string.Empty;
        }

        var trimmed = option.Trim();
        if (trimmed.Length > 2 &&
            (trimmed[0] is 'A' or 'B' or 'C' or 'D' or 'a' or 'b' or 'c' or 'd') &&
            (trimmed[1] is '.' or ')' or ':'))
        {
            return trimmed[2..].TrimStart();
        }

        return trimmed;
    }

    private static string NormalizeCorrectAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return "A";
        }

        var upper = answer.Trim().ToUpperInvariant();
        if (upper.StartsWith('A')) return "A";
        if (upper.StartsWith('B')) return "B";
        if (upper.StartsWith('C')) return "C";
        if (upper.StartsWith('D')) return "D";
        return "A";
    }
}
