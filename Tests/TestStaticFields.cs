using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    // II.10.5.3.2 describes how this should work...
    // In general, statics for a class need to be initialized when:
    // The value is constructed
    // Any static field is accessed
    // Any static method is invoked
    // The object is constructed
    // For value types, any method invocation
    public class TestStaticFields
    {
        public class MyClass
        {
            public MyClass()
            {
                TestApi.WriteLine("MyClass instantiated");
            }

            public void Test()
            {
                TestApi.WriteLine("MyClass.Test called");
            }
        }
        public const int MyConstField = 4;
        public static MyClass mcA = new MyClass();
        public static MyClass mcB = new MyClass();

        public void Run()
        {
            mcA.Test();
            mcB.Test();
        }
    }
}
