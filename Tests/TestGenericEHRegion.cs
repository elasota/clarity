using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestGenericEHRegion
    {
        public class MyClass<T>
                where T : class
        {
            public static void DoSomething(T[] arr)
            {
                try
                {
                    if (arr[0] == arr[1])
                        TestApi.WriteLine("Wrong");
                    TestApi.WriteLine("Wrong");
                }
                catch (IndexOutOfRangeException)
                {
                    TestApi.WriteLine("OK");
                }
            }
        }

        public void Run()
        {
            object[] objects = new object[1];
            objects[0] = null;

            MyClass<object>.DoSomething(objects);
        }
    }
}
