using RitsukageBot.Library.Utils;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RitsukageBot.Library.Graphic.Generators
{
    /// <summary>
    ///     Convert image to group cyan image.
    /// </summary>
    public static class GroupCyanImageConvertor
    {
        /// <summary>
        ///     Convert image to group cyan image.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Image<Rgba32> Convert(Image<Rgba32> source)
        {
            var image = source.Clone();
            image.RemoveGifGlobalColorTable();
            image.Mutate(ipc =>
            {
                var font1 = FontUtility.GetDefaultFont(80, FontStyle.Bold);
                var font2 = FontUtility.GetDefaultFont(38, FontStyle.Bold);
                var brush = Brushes.Solid(new Rgba32(255, 255, 255, 255));
                var pen = Pens.Solid(new Rgba32(84, 113, 176, 255), 2);
                Rectangle rect;
                if (image.Width > image.Height)
                {
                    var width = image.Height;
                    rect = new((image.Width - width) / 2, 0, width, image.Height);
                }
                else
                {
                    var height = image.Width;
                    rect = new(0, (image.Height - height) / 2, image.Width, height);
                }

                ipc.ProcessPixelRowsAsVector4(row =>
                {
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        var cyanValue = (pixel.X + pixel.Y + pixel.Z) / 3;
                        row[x] = new(cyanValue * 0.4f, cyanValue * 0.6f, cyanValue, pixel.W);
                    }
                });
                ipc.Crop(rect);
                ipc.Resize(500, 500);
                ipc.DrawText(new(font1)
                {
                    Origin = new PointF(435, 160),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }, "群\n青", brush, pen);
                ipc.DrawText(new(font2)
                {
                    Origin = new PointF(295, 310),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                }, "YOASOBI", brush, pen);
            });

            return image;
        }
    }
}