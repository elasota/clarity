using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNullableGeneric
    {
        public class NullChecker<T>
        {
            public static bool IsNull(T p)
            {
                return p == null;
            }
        }

        public void Run()
        {
            int? v = null;
            if (NullChecker<Nullable<int>>.IsNull(v))
                TestApi.WriteLine("OK");
            else
                TestApi.WriteLine("Wrong");
        }
    }
}
