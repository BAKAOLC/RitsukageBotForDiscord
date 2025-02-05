using Discord;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Color utility.
    /// </summary>
    public static class ColorUtility
    {
        /// <summary>
        ///     Transition between two colors.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="percent"></param>
        /// <returns></returns>
        public static Color Transition(Color from, Color to, double percent)
        {
            return percent switch
            {
                <= 0 => from,
                >= 1 => to,
                _ => new(
                    (byte)(from.R + (to.R - from.R) * percent),
                    (byte)(from.G + (to.G - from.G) * percent),
                    (byte)(from.B + (to.B - from.B) * percent)
                ),
            };
        }
    }
}