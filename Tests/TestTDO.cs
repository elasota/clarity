using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestTDO
    {
        public class Base
        {
        }
        public class Derived1 : Base
        {
        }
        public class Derived2 : Derived1
        {
        }

        public interface IMyInterface<in T>
        {
            void Func(T p);
        }

        public class BaseC : IMyInterface<Base>, IMyInterface<Derived1>
        {
            public virtual void Func(Base v)
            {
                TestApi.WriteLine("Wrong");
            }
            public virtual void Func(Derived1 v)
            {
                TestApi.WriteLine("Wrong");
            }
        }

        public class DerivedC : BaseC, IMyInterface<Derived1>, IMyInterface<Base>
        {
            public new virtual void Func(Derived1 v)
            {
                TestApi.WriteLine("Wrong");
            }
            public new virtual void Func(Base v)
            {
                TestApi.WriteLine("OK");
            }
        }

        public void Run()
        {
            ((IMyInterface<Derived2>)new DerivedC()).Func(null);
        }
    }
}
