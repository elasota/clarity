using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericInterfaceMethod
    {
        public interface IMyInterface
        {
            void Test<T>(T v);
        }

        public sealed class MyClass : IMyInterface
        {
            public void Test<T>(T v)
            {
                TestApi.WriteLine(v.ToString());
            }
        }

        private static void CallTest<T>(T v)
            where T : IMyInterface
        {
            v.Test<T>(v);
        }

        public void Run()
        {
            MyClass i = new MyClass();
            CallTest<MyClass>(i);
        }
    }
}
