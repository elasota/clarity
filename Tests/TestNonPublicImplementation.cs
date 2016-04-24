using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNonPublicImplementation
    {
        public interface IMyInterface
        {
            void Func();
        }

        public class MyBase : IMyInterface
        {
            public virtual void Func()
            {
                TestApi.WriteLine("OK");
            }
        }

        public class MyDerived : MyBase, IMyInterface
        {
            protected new virtual void Func()
            {
                TestApi.WriteLine("Wrong");
            }
        }

        public void Run()
        {
            MyDerived d = new MyDerived();
            ((IMyInterface)d).Func();
        }
    }
}
