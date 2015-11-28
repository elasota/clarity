using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestUnsignedToFloat
    {
        public void Run()
        {
            uint uintNumber = 2147483649;
            double dbl = (double)uintNumber;
            TestApi.WriteLine(dbl.ToString());
        }
    }
}
