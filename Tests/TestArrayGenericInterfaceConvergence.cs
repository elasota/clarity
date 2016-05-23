using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    // These test that AssemblyImporter's assignability checks are accurate
    public class TestArrayGenericInterfaceConvergence
    {
        public class MyClass
        {
        }
        public class MyDerived : MyClass
        {
        }

        public void SetAsList1(bool flag, MyClass[] a, IList<object> b, out IList<object> result)
        {
            result = flag ? a : b;
        }

        public void SetAsList2(bool flag, MyDerived[] a, IList<MyClass> b, out IList<MyClass> result)
        {
            result = flag ? a : b;
        }

        public void SetAsEnumerable(bool flag, MyClass[] a, IEnumerable b, out IEnumerable result)
        {
            result = flag ? a : b;
        }
    }
}
