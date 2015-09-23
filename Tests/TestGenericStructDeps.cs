using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericStructDeps
    {
        public class MyGeneric<T>
        {
            T value;
        }

        public struct MyStruct
        {
            int value;
        }

        public class MyClass1
            : MyGeneric<object>
        {
        }

        public class MyClass2
            : MyGeneric<MyStruct>
        {
        }
    }
}
