using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Utility class for handling strings
    /// </summary>
    public static partial class StringUtility
    {
        /// <summary>
        ///     Convert a string to a literal
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToLiteral(this string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var c in value)
                builder.Append(c.ToLiteral());
            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>
        ///     Convert a char to a literal
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToLiteral(this char value)
        {
            return value switch
            {
                '\0' => @"\0",
                '\a' => @"\a",
                '\b' => @"\b",
                '\t' => @"\t",
                '\n' => @"\n",
                '\v' => @"\v",
                '\f' => @"\f",
                '\r' => @"\r",
                '"' => "\\\"",
                '\\' => @"\\",
                _ => char.IsControl(value) ? $@"\u{(int)value:x4}" : value.ToString(),
            };
        }

        /// <summary>
        ///     Remove empty lines from a string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string RemoveEmptyLines(this string value)
        {
            return RemoveEmptyLines(value, ['\n', '\r']);
        }

        /// <summary>
        ///     Remove empty lines from a string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="separators"></param>
        /// <returns></returns>
        public static string RemoveEmptyLines(this string value, char[] separators)
        {
            var lines = value.Split(separators);
            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                builder.AppendLine(line);
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Encode a string to be used in a URL
        /// </summary>
        /// <param name="value"></param>
        /// <param name="toUpper"></param>
        /// <returns></returns>
        public static string UrlEncode(this string value, bool toUpper = true)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var encoded = HttpUtility.UrlPathEncode(value);
            return toUpper ? encoded.ToUpper() : encoded;
        }

        /// <summary>
        ///     Decode a URL-encoded string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string UrlDecode(this string value)
        {
            return HttpUtility.UrlDecode(value);
        }

        /// <summary>
        ///     Match URLs in a string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string[] MatchUrls(this string value)
        {
            return [.. GetUrlRegex().Matches(value).Select(x => x.Value)];
        }

        [GeneratedRegex(
            @"((https?|ftp|wss?|ws|file|data):\/\/)?(([a-zA-Z0-9\-_]+\.)+[a-zA-Z]{2,}|(\[[0-9a-fA-F:]+\])|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:[0-9]{1,5})?(\/[a-zA-Z0-9\-_\.~!$&'()*+,;=:@%]*)*(\?[a-zA-Z0-9\-_\.~!$&'()*+,;=:@%\/]*)?(#[a-zA-Z0-9\-_\.~!$&'()*+,;=:@%\/]*)?")]
        private static partial Regex GetUrlRegex();
    }
}