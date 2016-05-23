using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNewStruct
    {
        public struct MyStruct
        {
            private int a, b;

            public MyStruct(int pa, int pb)
            {
                a = pa;
                b = pb;
            }
        }

        public MyStruct Run()
        {
            return new MyStruct(1, 2);
        }
    }
}
