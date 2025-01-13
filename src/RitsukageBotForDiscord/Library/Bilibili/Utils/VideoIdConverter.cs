using System.Text.RegularExpressions;

namespace RitsukageBot.Library.Bilibili.Utils
{
    /// <summary>
    ///     Bilibili video id converter
    /// </summary>
    public static partial class VideoIdConverter
    {
        private const ulong Xor = 23442827791579UL;
        private const ulong Mask = 2251799813685247UL;
        private const ulong Aid = 1UL << 51;
        private const ulong Base = 58UL;

        private static readonly char[] CharSet =
            "FcwAPNKTMug3GV5Lj7EJnHpWsx4tb8haYeviqBz6rkCy12mUSDQX9RdoZf".ToCharArray();

        private static readonly Dictionary<char, int> CharValue = [];

        /// <summary>
        ///     Convert AV number to BV string
        /// </summary>
        /// <param name="av"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string ToBvid(ulong av)
        {
            if (av <= 0)
                throw new ArgumentOutOfRangeException(nameof(av), "av must be greater than 0");

            char[] result = ['B', 'V', '1', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' '];
            var idx = result.Length - 1;
            var tmp = (Aid | av) ^ Xor;

            while (tmp > 0)
            {
                result[idx] = CharSet[(int)(tmp % Base)];
                tmp /= Base;
                idx--;
            }

            (result[3], result[9]) = (result[9], result[3]);
            (result[4], result[7]) = (result[7], result[4]);

            return string.Join("", result);
        }

        /// <summary>
        ///     Convert BV string to AV number
        /// </summary>
        /// <param name="bv"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ulong ToAvid(string bv)
        {
            lock (CharValue)
            {
                if (CharValue.Count == 0)
                    for (var i = 0; i < CharSet.Length; i++)
                        CharValue[CharSet[i]] = i;
            }

            if (!GetBvCheckRegex1().IsMatch(bv))
            {
                if (!GetBvCheckRegex2().IsMatch(bv))
                    throw new ArgumentException(
                        "bv format is illegal, the correct bv format should be BV1xxxxxxxxx and meet the string set by base58 characters",
                        nameof(bv));
                bv = "BV" + bv;
            }

            var chars = bv.ToCharArray();

            (chars[3], chars[9]) = (chars[9], chars[3]);
            (chars[4], chars[7]) = (chars[7], chars[4]);

            chars = chars[3..];

            var av = chars.Aggregate(0UL, (current, c) => current * Base + (ulong)CharValue[c]);

            av = (av & Mask) ^ Xor;

            return av;
        }

        [GeneratedRegex("^[Bb][Vv]1[1-9a-km-zA-HJ-NP-Z]{9}$")]
        private static partial Regex GetBvCheckRegex1();

        [GeneratedRegex("^1[1-9a-km-zA-HJ-NP-Z]{9}$")]
        private static partial Regex GetBvCheckRegex2();
    }
}