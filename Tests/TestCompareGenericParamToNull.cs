using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestCompareGenericParamToNull
    {
        public class MyGeneric<T>
        {
            public static bool IsNull(T v)
            {
                return v == null;
            }
        }

        public void Run()
        {
            TestApi.WriteLine(MyGeneric<int>.IsNull(0).ToString());
            TestApi.WriteLine(MyGeneric<int>.IsNull(1).ToString());
            TestApi.WriteLine(MyGeneric<object>.IsNull(null).ToString());
        }
    }
}
