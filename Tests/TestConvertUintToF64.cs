using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestConvertUIntToF64
    {
        public void Run2()
        {
            int i = -2147483648;
            long l = i;
            TestApi.WriteLine(l.ToString());
        }

        public void Run()
        {
            uint ui = 2147483648;
            long l = ui;
            TestApi.WriteLine(l.ToString());

            Run2();
        }
    }
}
