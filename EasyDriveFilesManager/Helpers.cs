using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace EasyDriveFilesManager
{
    internal static class Helpers
    {
        internal static (List<T>, List<T>) Split<T>(this List<T> source, Func<T, bool> predicate)
        {
            var leftSide = new List<T>();
            var rightSide = new List<T>();
            foreach (var item in source)
            {
                if (predicate(item))
                {
                    leftSide.Add(item);
                }
                else
                {
                    rightSide.Add(item);
                }
            }
            return (leftSide, rightSide);
        }
    }
}
