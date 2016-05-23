using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNullable
    {
        public static Type GenReturnValueType<T>(T v)
        {
            return v.GetType();
        }

        public static Type ReturnValueType(int? v)
        {
            return v.GetType();
        }

        public void Run()
        {
            TestApi.WriteLine((GenReturnValueType<int?>(4) != typeof(int)) ? "OK" : "Wrong");
            TestApi.WriteLine((ReturnValueType(4) != typeof(int)) ? "OK" : "Wrong");
        }
    }
}
