using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 修刀列表 OCR 结果比较。
/// </summary>
public static class RepairListOcrDecision
{
    /// <summary>判断两次有效 OCR 是否表示列表没有变化。</summary>
    public static bool IsSameValidResult(string? previous, string? current)
    {
        var previousText = Normalize(previous);
        var currentText = Normalize(current);
        return previousText != null
            && currentText != null
            && previousText == currentText;
    }

    /// <summary>去除 OCR 文本中的空白，避免换行和空格影响比较。</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    }
}
