#if CLARITY_MULTIDIMENSIONAL_ARRAY

using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestMultidimensionalArray
    {
        public class MyClass
        {
        }

        public void TestCV(object[,] arr)
        {
        }

        public void TestCV2(MyClass[,] arr)
        {
            TestCV(arr);
        }

        public int Set(int a, int b, int c, int[,] array)
        {
            array[a, b] = c;
            return array[b, a];
        }

        public int SetLong(int a, long b, int c, int[,] array)
        {
            array[a, b] = c;
            return array[b, a];
        }

        public void Run()
        {
            int[,] test = new int[2, 2];
            Set(1, 0, 2, test);
            Set(0, 1, 3, test);
        }
    }
}

#endif
