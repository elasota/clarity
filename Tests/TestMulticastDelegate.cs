using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestMulticastDelegate
    {
        public delegate int MyMulticastDelegate(int a);
        public delegate int MyGenericDelegate<T>(T a);

        public int MyFunc1(int a)
        {
            TestApi.WriteLine("Func 1");
            return 1;
        }

        public int MyFunc2(int a)
        {
            TestApi.WriteLine("Func 2");
            return 2;
        }

        public int MyFunc3(int a)
        {
            TestApi.WriteLine("Func 3");
            return 3;
        }

        public void Run()
        {
            // This should only run Func 1 and Func 2
            MyMulticastDelegate d1 = MyFunc1;
            MyMulticastDelegate d2 = MyFunc2;
            d1 += d2;
            d2 += MyFunc3;
            d1(0);
        }
    }
}
