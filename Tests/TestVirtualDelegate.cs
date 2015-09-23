using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestVirtualDelegate
    {
        public abstract class Base
        {
            public abstract void Test();
        }

        public class Derived : Base
        {
            public override void Test()
            {
                TestApi.WriteLine("OK");
            }
        }

        public delegate void TestDelegate();

        public Base NewDerived()
        {
            return new Derived();
        }

        public void Run()
        {
            Base b = NewDerived();
            TestDelegate dg = b.Test;
            dg();
        }
    }
}
