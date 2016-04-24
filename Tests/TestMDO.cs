using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestMDO
    {
        // II.12.2.1
        public interface IMyInterface
        {
            void P(int p);
        }

        public class MyGeneric<A, B>
        {
            public virtual void P(A p)
            {
                TestApi.WriteLine("OK");
            }

            public virtual void P(B p)
            {
                TestApi.WriteLine("Wrong");
            }
        }

        public class MyClass : MyGeneric<int, int>, IMyInterface
        {
        }

        public void Run()
        {
            IMyInterface ifc = new MyClass();
            ifc.P(0);
        }
    }
}
