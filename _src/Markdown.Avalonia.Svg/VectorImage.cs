using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Svg.Skia;
using SkiaSharp;
using Svg.Skia;

namespace Markdown.Avalonia.Svg
{
    /// <summary>
    /// An <see cref="IImage"/> that uses a <see cref="SvgSource"/> for content.
    /// </summary>
    internal class VectorImage : IImage
    {
        /// <summary>
        /// Gets or sets the <see cref="SvgSource"/> content.
        /// </summary>
        public SvgSource? Source { get; set; }

        /// <inheritdoc/>
        public Size Size =>
            Source?.Picture is { } ? new Size(Source.Picture.CullRect.Width, Source.Picture.CullRect.Height) : default;

        /// <inheritdoc/>
        void IImage.Draw(
            DrawingContext context,
            Rect sourceRect,
            Rect destRect)
        {
            var source = Source;

            if (source?.Picture is null)
            {
                return;
            }

            if (Size.Width <= 0 || Size.Height <= 0)
            {
                return;
            }

            var bounds = source.Picture.CullRect;
            var scaleMatrix = Matrix.CreateScale(
                destRect.Width / sourceRect.Width,
                destRect.Height / sourceRect.Height);
            var translateMatrix = Matrix.CreateTranslation(
                -sourceRect.X + destRect.X - bounds.Top,
                -sourceRect.Y + destRect.Y - bounds.Left);
            using (context.PushClip(destRect))
            using (context.PushTransform(scaleMatrix * translateMatrix))
            {
                context.Custom(
                    new SvgSourceCustomDrawOperation(
                        new Rect(0, 0, bounds.Width, bounds.Height),
                        source));
            }
        }

    }
}