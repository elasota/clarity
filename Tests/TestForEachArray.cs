using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestForEachArray
    {
        public void Run()
        {
            string[] strArray = new string[] { "a", "b" };

            foreach (string str in strArray)
                TestApi.WriteLine(str);
        }
    }
}
