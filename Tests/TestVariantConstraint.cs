using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestVariantConstraint
    {
        public class MyBase
        {
            public string v;
        }

        public class MyDerived : MyBase
        {
        }

        public interface ITestConstraint<in T>
        {
            void Test(T v, MyBase b2);
        }

        public struct MyStruct : ITestConstraint<MyBase>
        {
            public void Test(MyBase b, MyBase b2)
            {
                TestApi.WriteLine(b.v);
            }
        }

        public class MyGeneric<T>
            where T : ITestConstraint<MyDerived>
        {
            public static void Test(ref T rs, MyDerived v)
            {
                rs.Test(v, null);
            }
        }

        public void Run()
        {
            MyStruct s;
            MyDerived d = new MyDerived();
            d.v = "OK";
            MyGeneric<MyStruct>.Test(ref s, d);
        }
    }
}
