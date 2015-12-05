using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestTypeOf
    {
        public class MyGeneric<T>
        {
        }

        public void Run()
        {
            Type t = typeof(void);
        }
    }
}
