using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestVirtualStructBoxing
    {
        // Tests the rules of I.8.6.1.5
        // Specifically, if a method is virtual, then "this" is boxed
        public interface MyInterface
        {
            object TestBoxed();
        }

        public struct MyStruct : MyInterface
        {
            public object TestUnboxed()
            {
                return this;
            }

            public object TestBoxed()
            {
                return this;
            }
        }

        public void Run()
        {
            MyStruct s;
            object v1 = s.TestUnboxed();
            object v2 = s.TestBoxed();
        }
    }
}
