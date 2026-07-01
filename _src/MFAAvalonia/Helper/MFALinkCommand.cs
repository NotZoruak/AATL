using Markdown.Avalonia.Utils;
using MFAAvalonia.Extensions;
using System;
using System.IO;
using System.Windows.Input;

namespace MFAAvalonia.Helper;

public class MFALinkCommand : ICommand
{
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;

    // 新增：当前Markdown文档的路径（作为解析相对链接的基准）
    public string? CurrentDocumentPath { get; set; } = AppPaths.DataRoot;

    public bool CanExecute(object? parameter) => true;

    // parameter为Markdown中的链接字符串（可能是相对路径或绝对路径）
    public void Execute(object? parameter)
    {
        var urlTxt = parameter as string;
        if (string.IsNullOrWhiteSpace(urlTxt))
            return;
        try
        {
            // 处理链接（区分相对路径和绝对路径）
            var resolvedUrl = urlTxt.ResolveUrl(CurrentDocumentPath);
            DefaultHyperlinkCommand.GoTo(resolvedUrl);
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"打开 Markdown 链接失败：链接={urlTxt}，原因={e.Message}", e);
        }
    }
}
