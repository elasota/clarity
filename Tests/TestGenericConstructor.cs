using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericConstructor
    {
        public class MyGeneric<T>
            where T : new()
        {
            public static T CreateNew()
            {
                return new T();
            }
        }

        public class MyClass
        {
        }

        public void Run()
        {
            int myInt = MyGeneric<int>.CreateNew();
            MyClass myClassInst = MyGeneric<MyClass>.CreateNew();
        }
    }
}
