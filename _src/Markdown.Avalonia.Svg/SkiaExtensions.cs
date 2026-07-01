using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace Markdown.Avalonia.Svg
{
    internal class SKBitmapDrawOperation : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }

        public SKBitmap? Bitmap { get; init; }

        public void Dispose()
        {
            //nop
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (Bitmap is SKBitmap bitmap && context.PlatformImpl.GetFeature<ISkiaSharpApiLeaseFeature>() is ISkiaSharpApiLeaseFeature leaseFeature)
            {
                ISkiaSharpApiLease lease = leaseFeature.Lease();
                using (lease)
                {
                    lease.SkCanvas.DrawBitmap(bitmap, SKRect.Create((float)Bounds.X, (float)Bounds.Y, (float)Bounds.Width, (float)Bounds.Height));
                }
            }
        }
    }

    static class SkiaExtensions
    {
      
        private class AvaloniaImage : IImage, IDisposable
        {
            private readonly SKBitmap? _source;
            SKBitmapDrawOperation? _drawImageOperation;

            public AvaloniaImage(SKBitmap? source)
            {
                _source = source;
                if (source?.Info.Size is SKSizeI size)
                {
                    Size = new(size.Width, size.Height);
                }
            }

            public Size Size { get; }

            public void Dispose() => _source?.Dispose();

            public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
            {
                if (_drawImageOperation is null)
                {
                    _drawImageOperation = new SKBitmapDrawOperation()
                    {
                        Bitmap = _source,
                    };
                }
                ;
                _drawImageOperation.Bounds = sourceRect;
                context.Custom(_drawImageOperation);
            }
        }

        public static SKBitmap? ToSKBitmap(this System.IO.Stream? stream)
        {
            if (stream == null)
                return null;
            return SKBitmap.Decode(stream);
        }

        public static IImage? ToAvaloniaImage(this SKBitmap? bitmap)
        {
            if (bitmap is not null)
            {
                return new AvaloniaImage(bitmap);
            }
            return default;
        }
    }
}
