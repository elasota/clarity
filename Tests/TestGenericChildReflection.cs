using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericChildReflection
    {
        public class MyGeneric<T>
        {
            public class MySubClass
            {
            }
        }

        public void Run()
        {
            Type t = typeof(MyGeneric<int>.MySubClass);
            Type t2 = t.ReflectedType;
        }
    }
}
