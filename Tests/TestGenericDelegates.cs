using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericDelegates
    {
        public class MyGeneric<T>
        {
            public virtual T Test(T p, ref T pr)
            {
                return p;
            }

            public M TestGenericMethod<M>(M p, ref M pr)
            {
                return p;
            }

            public delegate T TestDelegate(T p, ref T pr);
        }

        public class MyClass
        {
        }

        public struct MyStruct
        {
        }

        public void Run()
        {
            MyClass cinst = new MyClass();
            MyGeneric<MyClass> c = new MyGeneric<MyClass>();
            MyGeneric<MyClass>.TestDelegate dgc = c.Test;
            dgc(cinst, ref cinst);

            MyGeneric<int> genm = new MyGeneric<int>();
            MyGeneric<MyClass>.TestDelegate dggen = genm.TestGenericMethod<MyClass>;

            int iinst = 0;
            MyGeneric<int> i = new MyGeneric<int>();
            MyGeneric<int>.TestDelegate dgi = i.Test;
            dgi(iinst, ref iinst);

            MyStruct sinst = new MyStruct();
            MyGeneric<MyStruct> s = new MyGeneric<MyStruct>();
            MyGeneric<MyStruct>.TestDelegate dgs = s.Test;
            dgs(sinst, ref sinst);


        }
    }
}
