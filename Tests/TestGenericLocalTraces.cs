using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericLocalTraces
    {
        public class MyGeneric<T>
            where T : new()
        {
            public static void CallGC()
            {
                // TODO
            }

            public static T MakeTraced()
            {
                T v = new T();
                CallGC();
                return v;
            }
        }

        public class MyClass
        {
            public virtual void Test()
            {
                TestApi.WriteLine("OK");
            }
        }

        public void Run()
        {
            MyGeneric<MyClass>.MakeTraced().Test();
            int test2 = MyGeneric<int>.MakeTraced();
        }
    }
}
