using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestStructDelegate
    {
        public struct MyStruct
        {
            public string a, b;

            public void Test()
            {
                TestApi.WriteLine(b);
            }
        }

        public delegate void MyDelegate();

        public void Run()
        {
            MyStruct s = new MyStruct();
            s.b = "OK";

            MyDelegate dg = new MyDelegate(s.Test);
            dg();
        }
    }
}
