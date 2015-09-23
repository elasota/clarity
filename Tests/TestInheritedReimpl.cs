using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestInheritedReimpl
    {
        public interface MyInterface
        {
            void TestA();
            void TestB();
        }

        public class MyBase : MyInterface
        {
            public void TestA()
            {
            }

            void MyInterface.TestB()
            {
            }
        }

        public class MyDerived : MyBase, MyInterface
        {
            public void TestA()
            {
            }
        }
    }
}
