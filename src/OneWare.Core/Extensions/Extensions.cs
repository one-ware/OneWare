using System.Collections;

namespace OneWare.Core.Extensions
{
    public static class Extensions
    {
        public static bool IsDefault<T>(this T value) where T : struct
        {
            var isDefault = value.Equals(default(T));

            return isDefault;
        }

        public static bool IsNullOrEmpty(this IEnumerable? source)
        {
            if (source != null)
                foreach (var obj in source)
                    return false;
            return true;
        }
        
        public static void Each<T>(this IEnumerable<T> items, Action<T> action) {
            foreach (var item in items) {
                action(item);
            } 
        }
    }
}