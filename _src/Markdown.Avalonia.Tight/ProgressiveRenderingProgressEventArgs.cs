using System;

namespace Markdown.Avalonia
{
    /// <summary>
    /// 渐进式渲染进度事件参数
    /// </summary>
    public class ProgressiveRenderingProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 当前已渲染的行数
        /// </summary>
        public int RenderedLines { get; }

        /// <summary>
        /// 总行数
        /// </summary>
        public int TotalLines { get; }

        /// <summary>
        /// 渲染进度（0.0 - 1.0）
        /// </summary>
        public double Progress => TotalLines > 0 ? (double)RenderedLines / TotalLines : 1.0;

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => RenderedLines >= TotalLines;

        public ProgressiveRenderingProgressEventArgs(int renderedLines, int totalLines)
        {
            RenderedLines = renderedLines;
            TotalLines = totalLines;
        }
    }
}
