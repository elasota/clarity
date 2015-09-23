using System;
using System.Collections.Generic;

namespace Tests
{
    public class TestNewSlotImplementation
    {
        public interface MyInterface
        {
            void Test();
        }

        public class MyBase : MyInterface
        {
            public virtual void Test()
            {
                TestApi.WriteLine("MyBase");
            }
        }

        public class MyDerivedA : MyBase, MyInterface
        {
            public new virtual void Test()
            {
                TestApi.WriteLine("MyDerivedA");
            }
        }

        public class MyDerivedB : MyBase
        {
            public new virtual void Test()
            {
                TestApi.WriteLine("MyDerivedB");
            }
        }

        public void Run()
        {
            MyDerivedA da = new MyDerivedA();
            da.Test();
            ((MyInterface)da).Test();

            MyDerivedA db = new MyDerivedA();
            db.Test();
            ((MyInterface)db).Test();
        }
    }
}
