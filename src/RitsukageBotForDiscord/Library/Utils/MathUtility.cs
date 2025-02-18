namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Math Utility
    /// </summary>
    public static class MathUtility
    {
        /// <summary>
        ///     Greatest Common Divisor
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                var t = b;
                b = a % b;
                a = t;
            }

            return a;
        }
    }
}