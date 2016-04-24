using System;
using System.Collections.Generic;
using System.Text;

// Insane duplicate interface shit
//
// Despite post-substitution unification of interfaces on the same class being illegal,
// it is still possible for interfaces to duplicate if the interface was implemented
// by a base class.
//
// This appears to be an implementation bug that got patched out of .NET 3.5 SP1,
// then added back and due to complaints, and then standardized.
//
// Sigh.
//
// II.12.2
namespace Tests
{
    public class TestTDO
    {
        public interface IMyInterface<T>
        {
            void Func(T p);
        }

        public class Base<T> : IMyInterface<T>
        {
            public virtual void Func(T p)
            {
                TestApi.WriteLine("OK");
            }
        }

        public class Derived<T, U> : Base<T>, IMyInterface<U>
        {
            public virtual void Func(U p)
            {
                TestApi.WriteLine("Wrong");
            }

            public void CallU(U p)
            {
                IMyInterface<U> v = this;
                v.Func(p);
            }
        }

        public void Run()
        {
            Derived<int, int> v = new Derived<int, int>();
            v.CallU(0);
        }
    }
}
