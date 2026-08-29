using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public static class ResourcePointRewardParser
{
    public static IReadOnlyList<string> Parse(string text)
    {
        var parts = new List<string>();
        var matches = Regex.Matches(text, @"获得\s*(?<name>[^×xX\s]+)[×xX](?<count>\d+)");
        foreach (Match match in matches)
            parts.Add($"{match.Groups["name"].Value}x{match.Groups["count"].Value}");

        // 资源点中的委托符固定只会掉落一个；数量末位被 OCR 截断时按该规则补全。
        if (parts.Count == 0 && Regex.IsMatch(text, @"获得\s*委托符\s*[×xX]\s*$"))
            parts.Add("委托符x1");

        return parts;
    }
}
