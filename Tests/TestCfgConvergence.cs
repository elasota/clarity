using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestCfgConvergence
    {
        public interface MyInterface1 {}
        public interface MyInterface2 {}
        public class MyClass : MyInterface1, MyInterface2 {}

        public interface MyInterface3 : MyInterface1 { }
        public interface MyInterface4 : MyInterface1 { }

        public class MyBase { }
        public class MyDerived1 : MyBase { }
        public class MyDerived2 : MyBase { }

        public class MyGeneric<T>
            where T : MyInterface1, MyInterface2
        {
            public MyInterface1 Test(bool b, T v)
            {
                // Should demote to Object
                return b ? v : (MyInterface1)(new MyClass());
            }
        }

        public class MyGeneric2<T>
            where T : MyClass
        {
            public MyClass Test(bool b, T v)
            {
                // Should demote to MyClass
                return b ? v : new MyClass();
            }
        }

        public class MyGeneric3<T>
            where T : MyInterface3
        {
            public MyInterface1 Test(bool b, T v, MyInterface1 i1)
            {
                return b ? v : i1;
            }
        }

        public class MyGeneric4<T>
            where T : class
        {
            public object[] Test(bool b, T[] v1, object[] v2)
            {
                return b ? v1 : v2;
            }
        }

        public class MyGeneric5<T>
            where T : MyClass
        {
            public object[] Test(bool b, T[] v1, object[] v2)
            {
                return b ? v1 : v2;
            }
        }

        public class MyGeneric6<T>
            where T : MyDerived1
        {
            public MyBase Test(bool b, MyDerived1 v1, MyDerived2 v2)
            {
                return b ? (MyBase)v1 : (MyBase)v2;
            }
        }

        public class MyGeneric7<T>
            where T : struct
        {
            public object Test(bool b, T v1, System.ValueType v2)
            {
                return b ? v1 : v2;
            }
        }

        public void DoSomething(int a, int b, int c)
        {
        }

        public void TestMultiRegSpill(bool flag, int a, int b1, int b2, int c)
        {
            DoSomething(a, flag ? b1 : b2, c);
        }
    }
}
