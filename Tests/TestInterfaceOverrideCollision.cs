using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestInterfaceOverrideCollision
    {
        public interface MyInterfaceA<T>
        {
            void Test();
        }

        public interface MyInterfaceB
        {
            void Test();
        }

        public interface MyInterfaceC
        {
            void Test();
        }

        public abstract class MyBase
        {
            public abstract void Test();
        }

        public class MyDerived : MyBase, MyInterfaceA<int>, MyInterfaceB, MyInterfaceC
        {
            public override void Test()
            {
            }

            void MyInterfaceC.Test()
            {
            }
        }

        public void Run()
        {
            MyDerived d = new MyDerived();
            d.Test();
            ((MyInterfaceA<int>)d).Test();
            ((MyBase)d).Test();
        }
    }
}
