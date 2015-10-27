using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestValueTypeCalls
    {
        public struct MyStruct
        {
        }

        public enum MyEnum
        {
            Default = 0,
        }

        public void Run()
        {
            MyStruct s = new MyStruct();
            TestApi.WriteLine(s.GetType().ToString());
            TestApi.WriteLine(s.ToString());
            
            MyEnum e = MyEnum.Default;
            //TestApi.WriteLine(e.CompareTo(e).ToString());
            TestApi.WriteLine(e.GetTypeCode().ToString());
            TestApi.WriteLine(e.ToString());
        }
    }
}
