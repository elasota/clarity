using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestAbstractPartialImplementation
    {
        public interface MyInterface
        {
            void TestA();
            void TestB();
        }

        public abstract class MyBase : MyInterface
        {
            public void TestA() { }
            public abstract void TestB();
        }

        public abstract class MyDerived : MyInterface
        {
            public new virtual void TestA() { }
            public void TestB() { }
        }
    }
}
