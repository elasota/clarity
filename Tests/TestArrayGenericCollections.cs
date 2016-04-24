using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestArrayGenericCollections
    {
        public interface IMyInterface
        {
            string Value { get; }

        }

        public class MyClass : IMyInterface
        {
            public string Value { get; set; }
        }

        private void PrintList(IList<IMyInterface> v)
        {
            TestApi.WriteLine(v[0].Value);
        }

        public void Run()
        {
            MyClass[] array = new MyClass[1];
            array[0] = new MyClass();
            array[0].Value = "OK";
            PrintList(array);
        }
    }
}
