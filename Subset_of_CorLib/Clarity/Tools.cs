using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Clarity
{
    public class Tools
    {
        private static bool CompareEqual_IEquatable<T>(ref T a, ref T b)
            where T : IEquatable<T>
        {
            return a.Equals(b);
        }

        private static bool CompareEqual_General<T>(ref T a, ref T b)
        {
            return a.Equals(b);
        }

        // RLO-generated method that breaks the IEquatable constraint
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern static bool CompareEqual_Resolver<T>(ref T a, ref T b);

        public static bool CompareEqual<T>(T a, T b)
        {
            return CompareEqual_Resolver<T>(ref a, ref b);
        }

        public static object BoxNullable<T>(Nullable<T> v)
            where T : struct
        {
            if (v.HasValue)
                return (object)v.Value;
            return null;
        }
    }
}
