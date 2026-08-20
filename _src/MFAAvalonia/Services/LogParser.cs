using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Services;

/// <summary>日志行事件</summary>
public sealed record LogEntry(DateTime? Timestamp, string Level, string Content);

/// <summary>读取日志文件，解析为行事件流</summary>
public static class LogParser
{
    // 例：[2026-08-20 00:20:44.568][INF] [cfg=Default] [地下城] 出阵
    private static readonly Regex LineRegex = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]\[(?<level>[A-Z]+)\]\s*(?<content>.*)$",
        RegexOptions.Compiled);

    /// <summary>解析多行日志文本</summary>
    public static List<LogEntry> ParseLines(IEnumerable<string> lines)
    {
        var result = new List<LogEntry>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            var match = LineRegex.Match(trimmed);
            if (!match.Success)
                continue; // 时间戳缺失/格式异常的行跳过，不中断解析
            DateTime? ts = null;
            if (DateTime.TryParseExact(match.Groups["ts"].Value, "yyyy-MM-dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                ts = parsed;
            result.Add(new LogEntry(ts, match.Groups["level"].Value, match.Groups["content"].Value));
        }
        return result;
    }

    /// <summary>解析单个日志文件</summary>
    public static List<LogEntry> ParseFile(string path)
    {
        if (!File.Exists(path))
            return [];
        // 日志文件可能正被本进程的 Serilog 写入：显式共享读+写+删除，避免占用冲突
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return ParseLines(ReadAllLines(reader));
    }

    /// <summary>流式逐行读取（配合自定义 FileShare）</summary>
    private static IEnumerable<string> ReadAllLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}
