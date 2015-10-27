using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestStructBaseMethods
    {
        public struct MyStruct
        {
        }

        public void Run()
        {
            MyStruct s;
            TestApi.WriteLine(s.ToString());
        }
    }
}
