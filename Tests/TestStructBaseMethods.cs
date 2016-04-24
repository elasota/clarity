using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestStructBaseMethods
    {
        public struct MyStruct
        {
            public override string ToString()
            {
                return "OK";
            }
        }

        public void Run()
        {
            MyStruct s;
            TestApi.WriteLine(s.ToString());
        }
    }
}
