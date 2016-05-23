using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    // Insane undocumented shit that violates the standard (II.12.2).
    // If an interface METHOD is a reused implementation, then it's lower-priority than new implementations.
    public class TestInheritedImplementationDeprioritization
    {
        class A { }
        class B : A { }
        interface IVar<in T> { void P(T v, bool callB); }
        class S1
        {
            public virtual void P(B v, bool callB) { TestApi.WriteLine(callB ? "OK" : "Wrong"); }
            public virtual void P(A v, bool callB) { TestApi.WriteLine(callB ? "Wrong" : "OK"); }
        }
        class S2 : S1, IVar<B>
        {
        }
        class S3 : S2, IVar<B>, IVar<A>
        {
        }
        class S4 : S2, IVar<B>, IVar<A>
        {
            public new virtual void P(B v, bool callB) { TestApi.WriteLine(callB ? "OK" : "Wrong"); }
        }
        class S5 : S1, IVar<B>, IVar<A>
        {
        }

        class S6 : S1, IVar<B>
        {
        }

        class S7 : S6
        {
            public new virtual void P(B v, bool callB) { TestApi.WriteLine(callB ? "OK" : "Wrong"); }
        }

        class S8 : S7, IVar<B>, IVar<A>
        {
        }

        public void Run()
        {
            ((IVar<B>)new S3()).P(null, false);
            ((IVar<B>)new S4()).P(null, true);
            ((IVar<B>)new S5()).P(null, true);
            ((IVar<B>)new S8()).P(null, true);
        }
    }
}
