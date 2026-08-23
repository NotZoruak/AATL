using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 为刀剑掉落 OCR 提供受控的刀名相近字形匹配。
/// </summary>
public static class SwordNameMatcher
{
    private static readonly IReadOnlyDictionary<char, char> SimilarCharacters =
        new Dictionary<char, char>
        {
            ['掘'] = '堀',
            ['堀'] = '掘',
            ['広'] = '广',
            ['广'] = '広',
            ['國'] = '国',
            ['国'] = '國',
        };

    public static bool TryMatch(
        string recognizedName,
        string expectedType,
        IReadOnlyDictionary<string, string> swordTypeMap,
        out string canonicalName)
    {
        canonicalName = string.Empty;
        var normalized = Regex.Replace(recognizedName ?? string.Empty, @"\s+", string.Empty);
        if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(expectedType))
            return false;

        if (swordTypeMap.TryGetValue(normalized, out var exactType)
            && string.Equals(exactType, expectedType, StringComparison.Ordinal))
        {
            canonicalName = normalized;
            return true;
        }

        var candidates = swordTypeMap
            .Where(pair => string.Equals(pair.Value, expectedType, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .Where(candidate => IsSimilarName(normalized, candidate))
            .ToList();

        if (candidates.Count != 1)
            return false;

        canonicalName = candidates[0];
        return true;
    }

    private static bool IsSimilarName(string recognizedName, string candidate)
    {
        if (recognizedName.Length != candidate.Length)
            return false;

        var differences = 0;
        for (var i = 0; i < recognizedName.Length; i++)
        {
            if (recognizedName[i] == candidate[i])
                continue;

            if (!IsSimilarCharacter(recognizedName[i], candidate[i]))
                return false;

            differences++;
            if (differences > 1)
                return false;
        }

        return differences == 1;
    }

    private static bool IsSimilarCharacter(char left, char right) =>
        SimilarCharacters.TryGetValue(left, out var normalized) && normalized == right;
}
