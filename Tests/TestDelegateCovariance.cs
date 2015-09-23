using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestDelegateCovariance
    {
        // Covariance = returns more derived type
        public interface IBase
        {
            void TestIBase();
        }

        public interface IDerived : IBase
        {
        }

        public delegate IBase MyIDelegate();

        public static IBase MyIBaseFunc()
        {
            return null;
        }

        public static IDerived MyIDerivedFunc()
        {
            return null;
        }

        public class Base
        {
        }

        public class Derived : IDerived
        {
            public void TestIBase()
            {
            }
        }

        public void Run()
        {
            MyIDelegate dg = MyIDerivedFunc;
        }
    }
}
