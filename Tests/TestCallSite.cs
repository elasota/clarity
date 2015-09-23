using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestCallSite
    {
        public class Base
        {
            public void Test()
            {
                TestApi.WriteLine("OK");
            }
        }

        public class Derived : Base
        {
        }

        public void Run()
        {
            Derived d = new Derived();
            d.Test();
        }
    }
}
