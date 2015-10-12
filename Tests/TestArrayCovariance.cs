using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestArrayCovariance
    {
        public interface MyInterface
        {
            void Test();
        }

        public class MyBase
        {
        }

        public class MyClass : MyBase, MyInterface, MyDerivedInterface
        {
            public void Test() { TestApi.WriteLine("Right"); }
        }

        private static void PutObject(ref MyBase obj, MyClass inObj)
        {
            obj = inObj;
        }

        public interface MyBaseInterface
        {
        }

        public interface MyDerivedInterface : MyBaseInterface
        {
        }

        private void PassClassAsInterface(MyInterface[] arr)
        {
            arr[0].Test();
        }

        private void PassArrayOfArray(MyInterface[][] arr)
        {
            arr[0][0].Test();
        }

        public void Run()
        {
            MyClass c = new MyClass();
            MyInterface[] arr = new MyClass[4];
            arr[0] = c;

            MyBase[] arr2 = new MyClass[1];
            arr[0] = c;

            object[] arr3 = (object[])arr;
            arr3[1] = c;

            MyBaseInterface[] arr4 = new MyDerivedInterface[1];
            arr4[0] = c;

            object[] arr5 = new MyInterface[1];
            arr5[0] = c;

            MyClass[] arr6 = new MyClass[1];
            arr6[0] = c;
            PassClassAsInterface(arr6);

            MyClass[][] arr7 = new MyClass[1][];
            arr7[0] = new MyClass[1];
            arr7[0][0] = c;
            PassArrayOfArray(arr7);
        }
    }
}
