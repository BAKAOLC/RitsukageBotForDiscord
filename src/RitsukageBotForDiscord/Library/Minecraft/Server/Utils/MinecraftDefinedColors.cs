using System.Collections.Frozen;

namespace RitsukageBot.Library.Minecraft.Server.Utils
{
    /// <summary>
    ///     Minecraft defined colors
    /// </summary>
    public class MinecraftDefinedColors
    {
        /// <summary>
        ///     Minecraft defined colors
        /// </summary>
        public static readonly FrozenDictionary<char, string> Colors = new Dictionary<char, string>
        {
            { '0', "#000000" },
            { '1', "#0000AA" },
            { '2', "#00AA00" },
            { '3', "#00AAAA" },
            { '4', "#AA0000" },
            { '5', "#AA00AA" },
            { '6', "#FFAA00" },
            { '7', "#AAAAAA" },
            { '8', "#555555" },
            { '9', "#5555FF" },
            { 'a', "#55FF55" },
            { 'b', "#55FFFF" },
            { 'c', "#FF5555" },
            { 'd', "#FF55FF" },
            { 'e', "#FFFF55" },
            { 'f', "#FFFFFF" },
        }.ToFrozenDictionary();

        /// <summary>
        ///     Get color by char
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static string GetColor(char color)
        {
            return Colors.TryGetValue(color, out var value) ? value : "#FFFFFF";
        }
    }
}