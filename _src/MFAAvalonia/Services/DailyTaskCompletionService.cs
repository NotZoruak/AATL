using MFAAvalonia.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;

namespace MFAAvalonia.Services;

/// <summary>管理日课项目按游戏日完成一次的持久化状态。</summary>
public static class DailyTaskCompletionService
{
    private static readonly TimeOnly ResetTime = new(5, 0);
    private static readonly object SyncRoot = new();
    private const string CompletionLogFileName = "daily-task-completion.log";

    /// <summary>判断指定日课项目在当前游戏日是否仍应执行。</summary>
    public static bool ShouldRun(string item, DateTime now)
    {
        var gameDay = GetGameDay(now);
        lock (SyncRoot)
        {
            return !ReadRecords().Any(record =>
                string.Equals(record.Item, item, StringComparison.Ordinal)
                && string.Equals(record.GameDay, gameDay, StringComparison.Ordinal));
        }
    }

    /// <summary>将指定日课项目记录为当前游戏日已完成。</summary>
    public static void MarkCompleted(string item, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(item))
            throw new ArgumentException("日课项目标识不能为空。", nameof(item));

        var gameDay = GetGameDay(now);
        lock (SyncRoot)
        {
            var records = ReadRecords();
            if (records.Any(record =>
                    string.Equals(record.Item, item, StringComparison.Ordinal)
                    && string.Equals(record.GameDay, gameDay, StringComparison.Ordinal)))
                return;

            Directory.CreateDirectory(AppPaths.LogsDirectory);
            File.AppendAllText(
                GetLogPath(),
                $"{gameDay}\t{item}{Environment.NewLine}");
        }
    }

    private static string GetLogPath()
    {
        return Path.Combine(AppPaths.LogsDirectory, CompletionLogFileName);
    }

    private static IReadOnlyList<CompletionRecord> ReadRecords()
    {
        var path = GetLogPath();
        if (!File.Exists(path))
            return [];

        var records = new List<CompletionRecord>();
        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split('\t', 2, StringSplitOptions.TrimEntries);
            if (fields.Length == 2
                && !string.IsNullOrWhiteSpace(fields[0])
                && !string.IsNullOrWhiteSpace(fields[1]))
                records.Add(new CompletionRecord(fields[0], fields[1]));
        }

        return records;
    }

    private static string GetGameDay(DateTime now)
    {
        var localTime = now.Kind == DateTimeKind.Utc ? now.ToLocalTime() : now;
        if (localTime.TimeOfDay < ResetTime.ToTimeSpan())
            localTime = localTime.AddDays(-1);

        return DateOnly.FromDateTime(localTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record CompletionRecord(string GameDay, string Item);
}
