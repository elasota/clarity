using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestSlotDivergence
    {
        public interface MyInterfaceA
        {
            void Test();
        }

        public interface MyInterfaceB
        {
            void Test();
        }

        public class MyDerived : MyInterfaceA, MyInterfaceB
        {
            public virtual void Test()
            {
                TestApi.WriteLine("MyDerived implementation");
            }
        }

        public class MyDerived2 : MyDerived, MyInterfaceB
        {
            public override void Test()
            {
                TestApi.WriteLine("MyDerived2 implementation of hopefully A.test");
            }

            void MyInterfaceB.Test()
            {
                TestApi.WriteLine("MyDerived2 implementation of B.test");
            }
        }

        public void Run()
        {
            MyDerived d = new MyDerived2();
            MyDerived2 d2 = new MyDerived2();

            ((MyDerived)d2).Test();
            d2.Test();
            ((MyInterfaceB)(MyDerived)d2).Test();
        }
    }
}
