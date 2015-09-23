using System;
using System.Collections.Generic;

namespace Tests
{
    public class TestStructInterfaces
    {
        public interface MyInterface
        {
            void Test();
        }

        public interface MyGenericInterface<T>
        {
            void Test();
        }

        public struct MyStruct : MyInterface, MyGenericInterface<int>
        {
            public int i;

            void MyInterface.Test()
            {
                TestApi.WriteLine(i.ToString());
            }
            void MyGenericInterface<int>.Test()
            {
                TestApi.WriteLine(i.ToString());
            }
        }

        public void Run()
        {
            MyStruct s;
            s.i = 0;
            MyInterface i = (MyInterface)s;
            i.Test();
        }
    }
}
