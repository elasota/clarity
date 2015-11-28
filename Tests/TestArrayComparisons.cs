using System;
using System.Collections;

namespace Tests
{
    public class TestArrayComparisons
    {
        public bool CompareRefArrayToInterface1(object[] a, IEnumerable b)
        {
            return a == b;
        }

        public bool CompareRefArrayToInterface2(object[] a, IEnumerable b)
        {
            return b == a;
        }

        public class MyClass
        {
        }

        public void Run()
        {
            MyClass[] myClassArray = new MyClass[1];
            TestApi.WriteLine(CompareRefArrayToInterface1(myClassArray, myClassArray).ToString());
            TestApi.WriteLine(CompareRefArrayToInterface2(myClassArray, myClassArray).ToString());
        }
    }
}
