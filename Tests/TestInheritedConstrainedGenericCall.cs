using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestInheritedConstrainedGenericCall
    {
        public interface IMyInterfaceBase<in T>
        {
            void Test<M>(T v1, M v2);
        }

        public interface IMyInterfaceDerived<in T> : IMyInterfaceBase<T>
        {
        }

        public sealed class MyClass : IMyInterfaceDerived<object>
        {
            public void Test<M>(object v1, M v2)
            {
                TestApi.WriteLine(v1.ToString());
            }
        }


        public struct MyStruct : IMyInterfaceDerived<object>
        {
            public void Test<M>(object v1, M v2)
            {
                TestApi.WriteLine(v1.ToString());
            }
        }

        public class MyDispatcher<T> where T : IMyInterfaceBase<string>
        {
            public static void Dispatch(T v, string str)
            {
                v.Test<int>(str, 0);
            }
        }

        public void Run()
        {
            MyClass c = new MyClass();
            MyStruct s;

            MyDispatcher<MyClass>.Dispatch(c, "OK");
            //MyDispatcher<MyStruct>.Dispatch(s, "OK");
        }
    }
}
