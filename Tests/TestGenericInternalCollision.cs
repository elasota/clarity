using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericInternalCollision
    {
        public class MyGeneric<T>
            where T : new()
        {
            private static void TestOverloaded(T a)
            {
                TestApi.WriteLine("OK");
            }

            private static void TestOverloaded(int a)
            {
                TestApi.WriteLine("Bad");
            }

            public static void Test()
            {
                T a = new T();
                TestOverloaded(a);
            }
        }

        public void Run()
        {
            MyGeneric<int>.Test();
        }
    }
}
