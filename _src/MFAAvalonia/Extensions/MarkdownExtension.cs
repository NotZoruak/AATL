using Avalonia.Markup.Xaml;
using Markdown.Avalonia.Utils;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels.Windows;
using System;
using System.IO;

namespace MFAAvalonia.Extensions;

public class MarkdownExtension : MarkupExtension
{
    public string? Directory { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var resourcePath = AppPaths.ResourceDirectory;

        var targetDir = string.IsNullOrEmpty(Directory)
            ? resourcePath
            : Path.GetFullPath(Directory, AppPaths.DataRoot);

        // 创建 MFALinkCommand 并同步设置 CurrentDocumentPath
        var linkCommand = new MFALinkCommand
        {
            CurrentDocumentPath = targetDir
        };

        return new Markdown.Avalonia.Markdown
        {
            HyperlinkCommand = linkCommand,
            AssetPathRoot = targetDir,
            StrictBoldItalic = false,
        };
    }
}
