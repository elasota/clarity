using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestCgtUnRef
    {
        public static bool IsNotNull(object o)
        {
            return o != null;
        }

        public void Run()
        {
            TestApi.WriteLine(IsNotNull(null) ? "Wrong" : "OK");
        }
    }
}
