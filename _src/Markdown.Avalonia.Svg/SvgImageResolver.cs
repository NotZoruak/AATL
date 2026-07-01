using System;
using System.IO;
using System.Xml;
using Avalonia.Svg;
using Svg.Model;
using Avalonia.Media;
using Markdown.Avalonia.Utils;
using System.Threading.Tasks;
using Avalonia.Svg.Skia;

namespace Markdown.Avalonia.Svg
{
    internal class SvgImageResolver : IImageResolver
    {
        public async Task<IImage?> Load(Stream stream)
        {
            if (!IsSvgFile(stream))
                return null;

            SvgSource? source;
            try
            {
                source = await Task.Run(() => SvgSource.LoadFromStream(stream));
            }
            catch
            {
                return null;
            }

            return new SvgImage { Source = source };
        }

        private static bool IsSvgFile(Stream fileStream)
        {
            try
            {
                int firstChr = fileStream.ReadByte();
                if (firstChr != ('<' & 0xFF))
                    return false;

                fileStream.Seek(0, SeekOrigin.Begin);
                using (var xmlReader = XmlReader.Create(fileStream))
                {
                    return xmlReader.MoveToContent() == XmlNodeType.Element &&
                           "svg".Equals(xmlReader.Name, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                fileStream.Seek(0, SeekOrigin.Begin);
            }
        }
    }
}
