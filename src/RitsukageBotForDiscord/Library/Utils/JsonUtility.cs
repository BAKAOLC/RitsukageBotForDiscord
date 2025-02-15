using System.Text;
using Newtonsoft.Json;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Json Utility
    /// </summary>
    public static class JsonUtility
    {
        /// <summary>
        ///     Serialize object to json string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        ///     Deserialize json string to object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T? Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        ///     Return json string if it is valid, otherwise return empty string
        ///     It can also try to fix json string
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static string TryExtractVerifyJson(string original)
        {
            original = original.Trim();
            if (original is not ['{', ..] && original is not ['[', ..]) return string.Empty;
            var jsonStringBuilder = new StringBuilder();
            var depth = new Stack<char>();
            var inString = false;
            var isEscaping = false;
            foreach (var c in original)
            {
                if (c == '"' && !isEscaping) inString = !inString;
                if (c == '\\' && !isEscaping)
                    isEscaping = true;
                else
                    isEscaping = false;
                if (inString)
                {
                    jsonStringBuilder.Append(c);
                }
                else
                {
                    if (c is '{' or '[')
                    {
                        depth.Push(c);
                    }
                    else if (c is '}' or ']')
                    {
                        if (depth.Count == 0) break;
                        depth.Pop();
                    }

                    if (depth.Count > 0) jsonStringBuilder.Append(c);
                }
            }

            return depth.Count != 0 ? TryFixJson(original) : jsonStringBuilder.ToString();
        }

        /// <summary>
        ///     Try to fix json string
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static string TryFixJson(string original)
        {
            var jsonStringBuilder = new StringBuilder();
            var depth = new Stack<char>();
            var inString = false;
            var isEscaping = false;

            foreach (var c in original)
                switch (c)
                {
                    case '{':
                    case '[':
                        if (!inString)
                        {
                            depth.Push(c);
                            jsonStringBuilder.Append(c);
                        }

                        break;
                    case '}':
                    case ']':
                        if (!inString && depth.Count > 0)
                        {
                            var open = depth.Pop();
                            switch (open)
                            {
                                case '{':
                                    jsonStringBuilder.Append('}');
                                    break;
                                case '[':
                                    jsonStringBuilder.Append(']');
                                    break;
                            }
                        }

                        break;
                    case '"':
                        if (!isEscaping) inString = !inString;

                        jsonStringBuilder.Append(c);

                        break;
                    case '\\':
                        isEscaping = !isEscaping;
                        jsonStringBuilder.Append(c);
                        break;
                    default:
                        if (inString || !char.IsWhiteSpace(c))
                            jsonStringBuilder.Append(c);
                        break;
                }

            if (depth.Count <= 0) return jsonStringBuilder.ToString();
            {
                var open = depth.Pop();
                switch (open)
                {
                    case '{':
                        jsonStringBuilder.Append('}');
                        break;
                    case '[':
                        jsonStringBuilder.Append(']');
                        break;
                }
            }

            return jsonStringBuilder.ToString();
        }
    }
}