using SixLabors.Fonts;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Font utility.
    /// </summary>
    public static class FontUtility
    {
        /// <summary>
        ///     Gets the default font.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static Font GetDefaultFont(float size = 24, FontStyle style = FontStyle.Regular)
        {
            var font = (GetFont("Microsoft YaHei", size, style)
                        ?? GetFont("SimHei", size, style)
                        ?? GetFont("Arial", size, style))
                       ?? throw new FontFamilyNotFoundException(
                           "No default font found, please install Microsoft YaHei, SimHei or Arial.");
            return font;
        }

        private static Font? GetFont(string name, float size = 24, FontStyle style = FontStyle.Regular)
        {
            try
            {
                return SystemFonts.CreateFont(name, size, style);
            }
            catch
            {
                return null;
            }
        }
    }
}