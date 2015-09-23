using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNewSlot
    {
        public class Base
        {
            public virtual void Test()
            {
                TestApi.WriteLine("Right");
            }

            public virtual void Test2()
            {
                TestApi.WriteLine("Wrong");
            }

            public void InvokeTest()
            {
                Test();
            }
        }

        public class Derived : Base
        {
            public new virtual void Test()
            {
                TestApi.WriteLine("Wrong");
            }

            public sealed override void Test2()
            {
                TestApi.WriteLine("Right");
            }
        }

        public void Run()
        {
            Derived d = new Derived();
            d.InvokeTest();
            d.Test2();
        }
    }
}
