using System.Text;
using System.Text.RegularExpressions;
using StudyFlowAI.Models;

namespace StudyFlowAI.Services;

public readonly record struct TimeRange(TimeSpan Start, TimeSpan End)
{
    public int DurationMinutes => (int)Math.Round((End - Start).TotalMinutes);

    public bool Overlaps(TimeRange other) => Start < other.End && End > other.Start;

    public bool Contains(TimeRange inner) => Start <= inner.Start && End >= inner.End;
}

public sealed class WeeklyScheduleConstraints
{
    public Dictionary<DayOfWeek, List<TimeRange>> BusyByDay { get; } = CreateEmptyMap();
    public Dictionary<DayOfWeek, List<TimeRange>> FreeByDay { get; } = CreateEmptyMap();

    public bool HasAnyBusy => BusyByDay.Values.Any(list => list.Count > 0);
    public bool HasAnyFree => FreeByDay.Values.Any(list => list.Count > 0);

    private static Dictionary<DayOfWeek, List<TimeRange>> CreateEmptyMap() => new()
    {
        [DayOfWeek.Monday] = new List<TimeRange>(),
        [DayOfWeek.Tuesday] = new List<TimeRange>(),
        [DayOfWeek.Wednesday] = new List<TimeRange>(),
        [DayOfWeek.Thursday] = new List<TimeRange>(),
        [DayOfWeek.Friday] = new List<TimeRange>(),
        [DayOfWeek.Saturday] = new List<TimeRange>(),
        [DayOfWeek.Sunday] = new List<TimeRange>()
    };
}

public sealed class ScheduleValidationResult
{
    public List<KeHoachHocTap> AcceptedSessions { get; init; } = new();
    public int AdjustedCount { get; init; }
    public int DroppedCount { get; init; }
}

public static class ScheduleConstraintHelper
{
    private enum SlotKind
    {
        Unknown,
        Free,
        Busy
    }

    private static readonly DayOfWeek[] AllDays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private static readonly DayOfWeek[] Weekdays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday
    ];

    private static readonly DayOfWeek[] WeekendDays =
    [
        DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    // Khung giờ tự học mặc định dùng khi người dùng chỉ khai báo giờ BẬN mà không chỉ rõ giờ RẢNH
    private static readonly TimeRange[] DefaultStudyWindows =
    [
        new(new TimeSpan(19, 0, 0), new TimeSpan(22, 30, 0)),
        new(new TimeSpan(14, 0, 0), new TimeSpan(18, 0, 0)),
        new(new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))
    ];

    // Chỉ khớp các khoảng giờ có ký tự giờ rõ ràng (:, g, h, H) để tránh nhầm với "Thứ 2 - Thứ 6" hay "Tiết 1-3"
    private static readonly Regex TimeRangeRegex = new(
        @"(?<!\d)(?<sh>\d{1,2})(?:[:ghH](?<sm>\d{2})|[ghH])\s*(?:-|–|—|~|đến|tới|to)\s*(?<eh>\d{1,2})(?:[:ghH](?<em>\d{2})|[ghH])(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SentenceSplitRegex = new(
        @";|(?<!\d)\.(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex DayRangeRegex = new(
        @"(?:thứ\s*|t\s*)(?<d1>[2-7]|cn|chủ\s*nhật)\s*(?:-|–|—|~|đến|tới|to)\s*(?:thứ\s*|t\s*)?(?<d2>[2-7]|cn|chủ\s*nhật)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DayListRegex = new(
        @"(?:thứ\s+|t(?=[2-7]\b))(?<first>[2-7])(?<rest>(?:\s*(?:,|&|và)\s*(?:thứ\s+|t(?=[2-7]\b))?[2-7])*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SundayRegex = new(
        @"(?<![\p{L}\d])(?:chủ\s*nhật|cn)(?![\p{L}\d])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumberRegex = new(@"[2-7]", RegexOptions.Compiled);

    private static readonly Regex BusyKeywordRegex = new(
        @"(?<![\p{L}])(?:lịch\s*bận|bận\s*học|bận|không\s*rảnh|ko\s*rảnh|trên\s*trường|tkb|thời\s*khóa\s*biểu|làm\s*thêm|đi\s*làm|đi\s*học|đi\s*chơi|tránh)(?![\p{L}])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FreeKeywordRegex = new(
        @"(?<![\p{L}])(?:khung\s*giờ\s*rảnh|giờ\s*rảnh|rảnh|trống|tự\s*học|có\s*thể\s*học|ưu\s*tiên|học\s*bù|free)(?![\p{L}])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string GetVietnameseDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ 2",
        DayOfWeek.Tuesday => "Thứ 3",
        DayOfWeek.Wednesday => "Thứ 4",
        DayOfWeek.Thursday => "Thứ 5",
        DayOfWeek.Friday => "Thứ 6",
        DayOfWeek.Saturday => "Thứ 7",
        DayOfWeek.Sunday => "Chủ nhật",
        _ => ""
    };

    public static string BuildCalendarDayMapping(DateTime startDate, int totalDays)
    {
        var days = Math.Clamp(totalDays, 1, 31);
        var sb = new StringBuilder();
        for (var i = 0; i < days; i++)
        {
            var date = startDate.Date.AddDays(i);
            sb.AppendLine($"- {date:yyyy-MM-dd}: {GetVietnameseDayOfWeek(date.DayOfWeek)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static WeeklyScheduleConstraints Parse(string? rawScheduleText)
    {
        var constraints = new WeeklyScheduleConstraints();
        if (string.IsNullOrWhiteSpace(rawScheduleText))
        {
            return constraints;
        }

        var lines = rawScheduleText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // Xác định ngữ cảnh mặc định của cả dòng (ví dụ dòng "Lịch bận trên trường (từ TKB): ..." => Busy)
            var lineDefaultKind = DetectLineDefaultKind(line);
            var segments = SentenceSplitRegex.Split(line);

            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                var matches = TimeRangeRegex.Matches(segment);
                if (matches.Count == 0)
                {
                    continue;
                }

                var currentKind = lineDefaultKind;
                List<DayOfWeek>? currentDays = null;
                var prevSuffixConsumedLength = 0;

                for (var i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    if (!TryParseMatchToTimeRange(match, out var range))
                    {
                        prevSuffixConsumedLength = 0;
                        continue;
                    }

                    var rawPrefixStart = i == 0
                        ? 0
                        : matches[i - 1].Index + matches[i - 1].Length + prevSuffixConsumedLength;
                    if (rawPrefixStart > match.Index)
                    {
                        rawPrefixStart = match.Index;
                    }

                    var prefix = segment[rawPrefixStart..match.Index];

                    var suffixEnd = i + 1 < matches.Count ? matches[i + 1].Index : segment.Length;
                    var suffix = segment[(match.Index + match.Length)..suffixEnd];
                    var immediateSuffix = ExtractImmediateSuffix(suffix, out var consumedSuffixLen);
                    prevSuffixConsumedLength = consumedSuffixLen;

                    // Cập nhật thứ trong tuần nếu xuất hiện trước khoảng giờ (hoặc ngay sau nếu trước chưa có)
                    var daysInPrefix = ExtractDays(prefix);
                    if (daysInPrefix.Count > 0)
                    {
                        currentDays = daysInPrefix;
                    }
                    else if (currentDays is null)
                    {
                        var daysInSuffix = ExtractDays(immediateSuffix);
                        if (daysInSuffix.Count > 0)
                        {
                            currentDays = daysInSuffix;
                        }
                    }

                    var appliedDays = currentDays is { Count: > 0 } ? currentDays : AllDays.ToList();

                    // Xác định loại Busy / Free dựa trên từ khóa gần nhất trong prefix hoặc ngay sau khoảng giờ
                    var detectedKind = DetectSlotKind(prefix, immediateSuffix, currentKind);
                    if (detectedKind != SlotKind.Unknown)
                    {
                        currentKind = detectedKind;
                    }

                    var effectiveKind = currentKind == SlotKind.Unknown ? SlotKind.Free : currentKind;
                    var targetMap = effectiveKind == SlotKind.Busy ? constraints.BusyByDay : constraints.FreeByDay;

                    foreach (var day in appliedDays)
                    {
                        targetMap[day].Add(range);
                    }
                }
            }
        }

        // Chuẩn hóa: gộp các khoảng chồng lấn và trừ hoàn toàn khung giờ BẬN ra khỏi khung giờ RẢNH
        foreach (var day in AllDays)
        {
            var mergedBusy = MergeRanges(constraints.BusyByDay[day]);
            var mergedFree = MergeRanges(constraints.FreeByDay[day]);
            var cleanFree = SubtractRanges(mergedFree, mergedBusy);

            constraints.BusyByDay[day].Clear();
            constraints.BusyByDay[day].AddRange(mergedBusy);

            constraints.FreeByDay[day].Clear();
            constraints.FreeByDay[day].AddRange(cleanFree);
        }

        return constraints;
    }

    public static ScheduleValidationResult FilterAndAdjustSessions(
        List<KeHoachHocTap> candidateSessions,
        WeeklyScheduleConstraints constraints,
        IEnumerable<KeHoachHocTap>? existingSessions,
        DateTime minDate,
        DateTime maxDate)
    {
        var accepted = new List<KeHoachHocTap>();
        var adjustedCount = 0;
        var droppedCount = 0;

        var occupiedByDate = new Dictionary<DateTime, List<TimeRange>>();
        if (existingSessions is not null)
        {
            foreach (var existing in existingSessions)
            {
                var dateKey = existing.NgayHoc.Date;
                var end = existing.GioBatDau.Add(TimeSpan.FromMinutes(Math.Max(5, existing.ThoiLuongPhut)));
                if (end > TimeSpan.FromHours(24))
                {
                    end = TimeSpan.FromHours(24);
                }

                AddOccupied(occupiedByDate, dateKey, new TimeRange(existing.GioBatDau, end));
            }
        }

        foreach (var session in candidateSessions)
        {
            var date = session.NgayHoc.Date;
            if (date < minDate.Date || date > maxDate.Date)
            {
                droppedCount++;
                continue;
            }

            var durationMinutes = Math.Clamp(session.ThoiLuongPhut, 15, 240);
            session.ThoiLuongPhut = durationMinutes;

            var dayOfWeek = date.DayOfWeek;
            var busyRanges = constraints.BusyByDay[dayOfWeek];
            var freeRanges = constraints.FreeByDay[dayOfWeek];
            occupiedByDate.TryGetValue(date, out var occupiedInDay);

            var start = session.GioBatDau;
            var end = start.Add(TimeSpan.FromMinutes(durationMinutes));
            var slot = new TimeRange(start, end);

            var endsAfterMidnight = end > TimeSpan.FromHours(24);
            var overlapsBusy = busyRanges.Any(b => b.Overlaps(slot));
            var overlapsExisting = occupiedInDay is not null && occupiedInDay.Any(o => o.Overlaps(slot));
            var outsideExplicitFree = freeRanges.Count > 0 && !freeRanges.Any(f => f.Contains(slot));

            if (!endsAfterMidnight && !overlapsBusy && !overlapsExisting && !outsideExplicitFree)
            {
                accepted.Add(session);
                AddOccupied(occupiedByDate, date, slot);
                continue;
            }

            // Bị trùng giờ BẬN, trùng buổi khác hoặc ngoài giờ RẢNH -> tự động tìm khe giờ RẢNH hợp lệ trong cùng ngày
            if (TryFindAvailableStart(dayOfWeek, durationMinutes, constraints, occupiedInDay, out var newStart))
            {
                session.GioBatDau = newStart;
                var adjustedSlot = new TimeRange(newStart, newStart.Add(TimeSpan.FromMinutes(durationMinutes)));
                accepted.Add(session);
                AddOccupied(occupiedByDate, date, adjustedSlot);
                adjustedCount++;
            }
            else
            {
                // Ngày này không còn khung giờ rảnh đủ dài -> bỏ qua buổi lỗi, tuyệt đối không lưu trùng giờ bận
                droppedCount++;
            }
        }

        return new ScheduleValidationResult
        {
            AcceptedSessions = accepted,
            AdjustedCount = adjustedCount,
            DroppedCount = droppedCount
        };
    }

    private static bool TryFindAvailableStart(
        DayOfWeek dayOfWeek,
        int durationMinutes,
        WeeklyScheduleConstraints constraints,
        List<TimeRange>? occupiedInDay,
        out TimeSpan newStart)
    {
        var busyRanges = constraints.BusyByDay[dayOfWeek];
        var explicitFreeRanges = constraints.FreeByDay[dayOfWeek];

        // Nếu ngày đó có khung giờ RẢNH tường minh thì chỉ chọn trong khung RẢNH (đã trừ BẬN sẵn)
        // Nếu người dùng chỉ khai báo giờ BẬN mà không ghi giờ RẢNH -> dùng các khung giờ tự học mặc định đã trừ BẬN
        var candidateWindows = explicitFreeRanges.Count > 0
            ? explicitFreeRanges
            : SubtractRanges(DefaultStudyWindows, busyRanges);

        if (candidateWindows.Count == 0)
        {
            newStart = default;
            return false;
        }

        // Thử tìm khe trống có chừa khoảng nghỉ 10 phút trước; nếu không có thì thử khít không giao nhau
        foreach (var bufferMinutes in new[] { 10, 0 })
        {
            var blocked = new List<TimeRange>(busyRanges);
            if (occupiedInDay is not null)
            {
                foreach (var occ in occupiedInDay)
                {
                    var bufferedStart = occ.Start - TimeSpan.FromMinutes(bufferMinutes);
                    if (bufferedStart < TimeSpan.Zero) bufferedStart = TimeSpan.Zero;
                    var bufferedEnd = occ.End + TimeSpan.FromMinutes(bufferMinutes);
                    if (bufferedEnd > TimeSpan.FromHours(24)) bufferedEnd = TimeSpan.FromHours(24);
                    blocked.Add(new TimeRange(bufferedStart, bufferedEnd));
                }
            }

            var mergedBlocked = MergeRanges(blocked);
            var availableChunks = SubtractRanges(candidateWindows, mergedBlocked);

            foreach (var chunk in availableChunks)
            {
                if (chunk.DurationMinutes >= durationMinutes)
                {
                    newStart = chunk.Start;
                    return true;
                }
            }
        }

        newStart = default;
        return false;
    }

    private static SlotKind DetectLineDefaultKind(string line)
    {
        var colonIndex = line.IndexOf(':');
        var header = colonIndex > 0 && colonIndex <= 60 ? line[..colonIndex] : line;

        var hasBusyInHeader = BusyKeywordRegex.IsMatch(header);
        var hasFreeInHeader = FreeKeywordRegex.IsMatch(header);

        if (hasBusyInHeader && !hasFreeInHeader)
        {
            return SlotKind.Busy;
        }

        if (hasFreeInHeader && !hasBusyInHeader)
        {
            return SlotKind.Free;
        }

        if (hasBusyInHeader && hasFreeInHeader)
        {
            var lastBusy = BusyKeywordRegex.Matches(header)[^1].Index;
            var lastFree = FreeKeywordRegex.Matches(header)[^1].Index;
            return lastBusy > lastFree ? SlotKind.Busy : SlotKind.Free;
        }

        return SlotKind.Free;
    }

    private static SlotKind DetectSlotKind(string prefix, string immediateSuffix, SlotKind fallback)
    {
        // Ưu tiên từ khóa xuất hiện gần nhất ngay trước khoảng giờ
        var prefixBusy = BusyKeywordRegex.Matches(prefix);
        var prefixFree = FreeKeywordRegex.Matches(prefix);

        if (prefixBusy.Count > 0 || prefixFree.Count > 0)
        {
            var lastBusyIdx = prefixBusy.Count > 0 ? prefixBusy[^1].Index : -1;
            var lastFreeIdx = prefixFree.Count > 0 ? prefixFree[^1].Index : -1;
            return lastBusyIdx > lastFreeIdx ? SlotKind.Busy : SlotKind.Free;
        }

        // Kiểm tra ghi chú ngay sau khoảng giờ (VD: "06:00-11:30 là BẬN" hoặc "07:30-11:30 (Bận học)")
        var suffixBusy = BusyKeywordRegex.Matches(immediateSuffix);
        var suffixFree = FreeKeywordRegex.Matches(immediateSuffix);

        if (suffixBusy.Count > 0 || suffixFree.Count > 0)
        {
            var firstBusyIdx = suffixBusy.Count > 0 ? suffixBusy[0].Index : int.MaxValue;
            var firstFreeIdx = suffixFree.Count > 0 ? suffixFree[0].Index : int.MaxValue;
            return firstBusyIdx < firstFreeIdx ? SlotKind.Busy : SlotKind.Free;
        }

        return fallback;
    }

    private static string ExtractImmediateSuffix(string suffix, out int consumedLength)
    {
        consumedLength = 0;
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return string.Empty;
        }

        var cutIdx = suffix.Length;
        string[] delimiters = [",", " và ", " nhưng ", " còn ", " / "];
        foreach (var delim in delimiters)
        {
            var idx = suffix.IndexOf(delim, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < cutIdx)
            {
                cutIdx = idx;
            }
        }

        var maxLen = Math.Min(cutIdx, 45);
        consumedLength = maxLen;
        return suffix[..maxLen];
    }

    private static List<DayOfWeek> ExtractDays(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<DayOfWeek>();
        }

        var found = new HashSet<DayOfWeek>();

        // 1. Khoảng thứ: Thứ 2 - Thứ 6, T2-T6, Thứ 7 - Chủ nhật...
        foreach (Match m in DayRangeRegex.Matches(text))
        {
            if (TryMapVietnameseDayToken(m.Groups["d1"].Value, out var d1) &&
                TryMapVietnameseDayToken(m.Groups["d2"].Value, out var d2))
            {
                var idx1 = DayToMondayIndex(d1);
                var idx2 = DayToMondayIndex(d2);
                if (idx1 <= idx2)
                {
                    for (var idx = idx1; idx <= idx2; idx++)
                    {
                        found.Add(AllDays[idx]);
                    }
                }
                else
                {
                    for (var idx = idx1; idx < 7; idx++) found.Add(AllDays[idx]);
                    for (var idx = 0; idx <= idx2; idx++) found.Add(AllDays[idx]);
                }
            }
        }

        // 2. Danh sách thứ: Thứ 2, 4, 6 | Thứ 3 & Thứ 5 | T2, T4
        foreach (Match m in DayListRegex.Matches(text))
        {
            if (TryMapVietnameseDayToken(m.Groups["first"].Value, out var firstDay))
            {
                found.Add(firstDay);
            }

            var rest = m.Groups["rest"].Value;
            if (!string.IsNullOrWhiteSpace(rest))
            {
                foreach (Match num in NumberRegex.Matches(rest))
                {
                    if (TryMapVietnameseDayToken(num.Value, out var extraDay))
                    {
                        found.Add(extraDay);
                    }
                }
            }
        }

        // 3. Chủ nhật / CN
        if (SundayRegex.IsMatch(text))
        {
            found.Add(DayOfWeek.Sunday);
        }

        // 4. Nhóm ngày tiếng Việt
        var lower = text.ToLowerInvariant();
        if (lower.Contains("cuối tuần"))
        {
            foreach (var d in WeekendDays) found.Add(d);
        }

        if (lower.Contains("trong tuần") || lower.Contains("ngày thường"))
        {
            foreach (var d in Weekdays) found.Add(d);
        }

        if (lower.Contains("mỗi ngày") ||
            lower.Contains("hàng ngày") ||
            lower.Contains("tất cả các ngày") ||
            lower.Contains("các tối") ||
            lower.Contains("các buổi tối") ||
            lower.Contains("các buổi sáng") ||
            lower.Contains("các buổi chiều"))
        {
            foreach (var d in AllDays) found.Add(d);
        }

        return AllDays.Where(found.Contains).ToList();
    }

    private static bool TryParseMatchToTimeRange(Match match, out TimeRange range)
    {
        range = default;
        if (!int.TryParse(match.Groups["sh"].Value, out var sh) ||
            !int.TryParse(match.Groups["eh"].Value, out var eh))
        {
            return false;
        }

        var sm = 0;
        if (match.Groups["sm"].Success && !int.TryParse(match.Groups["sm"].Value, out sm))
        {
            return false;
        }

        var em = 0;
        if (match.Groups["em"].Success && !int.TryParse(match.Groups["em"].Value, out em))
        {
            return false;
        }

        if (sh < 0 || sh > 23 || sm < 0 || sm > 59)
        {
            return false;
        }

        if (eh < 0 || eh > 24 || em < 0 || em > 59 || (eh == 24 && em > 0))
        {
            return false;
        }

        var start = new TimeSpan(sh, sm, 0);
        var end = new TimeSpan(eh, em, 0);
        if (end <= start)
        {
            return false;
        }

        range = new TimeRange(start, end);
        return true;
    }

    private static bool TryMapVietnameseDayToken(string raw, out DayOfWeek day)
    {
        var token = raw.Trim().ToLowerInvariant();
        switch (token)
        {
            case "2": day = DayOfWeek.Monday; return true;
            case "3": day = DayOfWeek.Tuesday; return true;
            case "4": day = DayOfWeek.Wednesday; return true;
            case "5": day = DayOfWeek.Thursday; return true;
            case "6": day = DayOfWeek.Friday; return true;
            case "7": day = DayOfWeek.Saturday; return true;
            case "cn":
            case "chủ nhật":
            case "chủnhật":
                day = DayOfWeek.Sunday; return true;
            default:
                if (token.StartsWith("chủ") && token.EndsWith("nhật"))
                {
                    day = DayOfWeek.Sunday;
                    return true;
                }

                day = default;
                return false;
        }
    }

    private static int DayToMondayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 0,
        DayOfWeek.Tuesday => 1,
        DayOfWeek.Wednesday => 2,
        DayOfWeek.Thursday => 3,
        DayOfWeek.Friday => 4,
        DayOfWeek.Saturday => 5,
        DayOfWeek.Sunday => 6,
        _ => 0
    };

    private static List<TimeRange> MergeRanges(IEnumerable<TimeRange> ranges)
    {
        var sorted = ranges
            .Where(r => r.End > r.Start)
            .OrderBy(r => r.Start)
            .ThenBy(r => r.End)
            .ToList();

        if (sorted.Count <= 1)
        {
            return sorted;
        }

        var merged = new List<TimeRange> { sorted[0] };
        for (var i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var last = merged[^1];
            if (current.Start <= last.End)
            {
                merged[^1] = new TimeRange(last.Start, current.End > last.End ? current.End : last.End);
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static List<TimeRange> SubtractRanges(IEnumerable<TimeRange> source, IEnumerable<TimeRange> subtract)
    {
        var blockers = MergeRanges(subtract);
        var current = source.Where(r => r.End > r.Start).ToList();

        foreach (var b in blockers)
        {
            var next = new List<TimeRange>();
            foreach (var r in current)
            {
                if (!r.Overlaps(b))
                {
                    next.Add(r);
                    continue;
                }

                if (r.Start < b.Start)
                {
                    next.Add(new TimeRange(r.Start, b.Start));
                }

                if (r.End > b.End)
                {
                    next.Add(new TimeRange(b.End, r.End));
                }
            }

            current = next;
        }

        return current;
    }

    private static void AddOccupied(Dictionary<DateTime, List<TimeRange>> map, DateTime date, TimeRange range)
    {
        if (!map.TryGetValue(date, out var list))
        {
            list = new List<TimeRange>();
            map[date] = list;
        }

        list.Add(range);
    }
}
