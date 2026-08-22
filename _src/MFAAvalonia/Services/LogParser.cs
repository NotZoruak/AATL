using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Services;

/// <summary>日志行事件</summary>
/// <param name="ConfigSource">配置来源（[cfg=xxx] 块的原值，无则 null）</param>
/// <param name="InstanceId">实例 ID（[inst=显示名/实例ID] 中斜杠后的部分，无则 null）</param>
public sealed record LogEntry(
    DateTime? Timestamp,
    string Level,
    string Content,
    string? ConfigSource = null,
    string? InstanceId = null);

/// <summary>读取日志文件，解析为行事件流</summary>
public static class LogParser
{
    // 例：[2026-08-20 00:20:44.568][INF] [cfg=Default][inst=一号机/default] [地下城] 出阵
    private static readonly Regex LineRegex = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]\[(?<level>[A-Z]+)\]\s*(?<content>.*)$",
        RegexOptions.Compiled);

    // 上下文块中的配置来源：[cfg=配置名]
    private static readonly Regex ConfigRegex = new(
        @"\[cfg=([^\]]*)\]",
        RegexOptions.Compiled);

    private static readonly Regex InstanceRegex = new(
        @"\[inst=[^\]/]+/(?<id>[^\]]+)\]",
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
            var content = match.Groups["content"].Value;
            var config = ConfigRegex.Match(content).Groups[1].Value;
            var instance = InstanceRegex.Match(content).Groups["id"].Value;
            result.Add(new LogEntry(ts, match.Groups["level"].Value, content,
                string.IsNullOrWhiteSpace(config) ? null : config,
                string.IsNullOrWhiteSpace(instance) ? null : instance));
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
