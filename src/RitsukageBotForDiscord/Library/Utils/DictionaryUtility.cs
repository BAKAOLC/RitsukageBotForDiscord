using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RitsukageBot.Library.Utils
{
    /// <summary>
    ///     Utility class for <see cref="Dictionary{TKey, TValue}" />.
    /// </summary>
    public static class DictionaryUtility
    {
        /// <summary>
        ///     Get the value associated with the specified key or add a new value if the key does not exist.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static TValue? GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue? value)
            where TKey : notnull
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(dictionary, key, out var exists);
            if (!exists) val = value;

            return val;
        }

        /// <summary>
        ///     Update the value associated with the specified key if the key exists.
        ///     Returns true if the key exists and the value is updated; otherwise, false.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : notnull
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(dictionary, key);
            if (Unsafe.IsNullRef(ref val)) return false;

            val = value;
            return true;
        }
    }
}