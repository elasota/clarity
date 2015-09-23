using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestInterfaceInheritance
    {
        public interface IMyInterfaceA
        {
            void TestA();
        }

        public interface IMyInterfaceB : IMyInterfaceA
        {
            void TestB();
        }

        public interface IMyInterfaceC : IMyInterfaceA
        {
            void TestC();
        }

        public class MyClass : IMyInterfaceB, IMyInterfaceC
        {
            void IMyInterfaceA.TestA() { }
            public void TestB() { }
            public void TestC() { }
        }

        public interface IMyParentGenericInterface<T>
        {
            void Test(T v);
        }

        public interface IMyGenericInterface<T> : IMyParentGenericInterface<T>
        {
            void Test2(int v);
        }

        public class MyImplementsGeneric : IMyGenericInterface<int>, IMyGenericInterface<bool>
        {
            void IMyParentGenericInterface<int>.Test(int a)
            {
            }

            void IMyParentGenericInterface<bool>.Test(bool a)
            {
            }

            void IMyGenericInterface<int>.Test2(int a)
            {
            }

            void IMyGenericInterface<bool>.Test2(int a)
            {
            }
        }
        

        public interface MyIfcChainA
        {
            void TestA();
        }

        public interface MyIfcChainB : MyIfcChainA
        {
        }

        public interface MyIfcChainC : MyIfcChainB
        {
        }

        public class MyChainingClass : MyIfcChainC
        {
            public void TestA()
            {
            }
        }

        public void Run()
        {
            IMyInterfaceB b = new MyClass();
            IMyInterfaceA a = b;
            a.TestA();

            MyIfcChainC c = new MyChainingClass();
            MyIfcChainA ca = c;
            ca.TestA();
        }
    }
}
