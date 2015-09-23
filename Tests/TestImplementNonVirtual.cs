using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestImplementNonVirtual
    {
		public interface MyInterface
        {
            void Test();
        }

		public class MyBase
        {
            public void Test()
            {
                TestApi.WriteLine("Test");
            }
        }

        public class MyDerived : MyBase, MyInterface
        {
        }

        public void Run()
        {
            MyDerived d = new MyDerived();
            ((MyInterface)d).Test();
        }
    }
}
