using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestVariance
    {
        public class MyBase
        {
            public virtual void BaseFunc()
            {
                TestApi.WriteLine("Wrong");
            }
        }

        public class MySubclass : MyBase
        {
            public override void BaseFunc()
            {
                TestApi.WriteLine("OK");
            }
        }

        public interface IMyInterface<out TReturn, in TParam>
        {
            TReturn Func(TParam p);
        }

        public class MyClass : IMyInterface<MySubclass, MyBase>
        {
            public virtual MySubclass Func(MyBase p)
            {
                TestApi.WriteLine("OK");
                return new MySubclass();
            }
        }

        public delegate TOut MyOutDelegate<out TOut>();
        public delegate void MyInDelegate<in TIn>(TIn p);
        public delegate TOut MyInOutDelegate<out TOut, in TIn>(TIn p);

        public interface IMyInInterface<in T>
        {
            void Func(T p);
        }

        public interface IMyOutInterface<out T>
        {
        }

        public class MyTestClass : IMyInInterface<MyInDelegate<MySubclass>>
        {
            public void Func(MyInDelegate<MySubclass> mySubclass)
            {
                mySubclass(new MySubclass());
            }
        }

        public void Run()
        {
            IMyInterface<MyBase, MySubclass> b = new MyClass();

            MyInOutDelegate<MySubclass, MyBase> dgA = null;
            MyInOutDelegate<MyBase, MySubclass> dgB = dgA;

            IMyInInterface<MyInDelegate<MySubclass>> ifcA = new MyTestClass();
            IMyInInterface<MyInDelegate<MyBase>> ifcB = ifcA;

            MyInDelegate<MyBase> inA = null;
            MyInDelegate<MySubclass> inB = inA;

            IMyInInterface<MyBase> ifc2A = null;
            IMyInInterface<MySubclass> ifc2B = ifc2A;

            b.Func(null).BaseFunc();
        }
    }
}
