using ColorDocument.Avalonia;
using ColorDocument.Avalonia.DocumentElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Markdown.Avalonia.Parsers.Builtin
{
    internal class BlockquotesParser : BlockParser2
    {
        private static readonly Regex _blockquoteFirst = new(@"
            ^
            ([>].*)
            (\n[>].*)*
            [\n]*
            ", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        // GitHub-style alert pattern: [!NOTE], [!TIP], [!IMPORTANT], [!WARNING], [!CAUTION]
        private static readonly Regex _alertPattern = new(@"^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _supportTextAlignment;

        public BlockquotesParser(bool supportTextAlignment) : base(_blockquoteFirst, "BlockquotesEvaluator")
        {
            _supportTextAlignment = supportTextAlignment;
        }

        public override IEnumerable<DocumentElement>? Convert2(string text, Match firstMatch, ParseStatus status, IMarkdownEngine2 engine, out int parseTextBegin, out int parseTextEnd)
        {
            parseTextBegin = firstMatch.Index;
            parseTextEnd = firstMatch.Index + firstMatch.Length;

            // trim '>'
            var lines = firstMatch.Value.Trim().Split('\n')
                .Select(txt =>
                {
                    if (txt.Length <= 1) return string.Empty;
                    var trimmed = txt.Substring(1);
                    if (trimmed.FirstOrDefault() == ' ') trimmed = trimmed.Substring(1);
                    return trimmed;
                })
                .ToArray();

            // Check if first line is a GitHub-style alert marker
            if (lines.Length > 0)
            {
                var alertMatch = _alertPattern.Match(lines[0]);
                if (alertMatch.Success)
                {
                    var alertTypeStr = alertMatch.Groups[1].Value.ToUpperInvariant();
                    var alertType = alertTypeStr switch
                    {
                        "TIP" => AlertType.Tip,
                        "IMPORTANT" => AlertType.Important,
                        "WARNING" => AlertType.Warning,
                        "CAUTION" => AlertType.Caution,
                        _ => AlertType.Note
                    };

                    // Get content after the alert marker (skip first line)
                    var contentLines = lines.Skip(1).ToArray();
                    var trimmedTxt = string.Join("\n", contentLines);

                    var newStatus = new ParseStatus(true & _supportTextAlignment);
                    var blocks = engine.ParseGamutElement(trimmedTxt + "\n", newStatus);

                    return new[] { new AlertBlockElement(blocks, alertType) };
                }
            }

            // Regular blockquote
            var trimmedText = string.Join("\n", lines);

            var regularStatus = new ParseStatus(true & _supportTextAlignment);
            var regularBlocks = engine.ParseGamutElement(trimmedText + "\n", regularStatus);

            return new[] { new BlockquoteElement(regularBlocks) };
        }
    }
}

