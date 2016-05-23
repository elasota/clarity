using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestArrayConstrainedToInterface
    {
        public class MyGeneric<T>
            where T : IEnumerable<string>
        {
            public static void Print(T v)
            {
                foreach (string str in v)
                    TestApi.WriteLine(str);
            }
        }

        public void Run()
        {
            string[] strs = new string[1];
            strs[0] = "OK";
            MyGeneric<string[]>.Print(strs);
        }
    }
}
