using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestDelegateContravariance
    {
        // Contravariance = accepts less derived type
        public interface IBase
        {
        }

        public interface IDerived : IBase
        {
        }

        public static void MyIFunc(IBase derived)
        {
        }

        public delegate void MyIDelegate(IDerived b);

        public void DoSomething(MyIDelegate myDG)
        {
        }

        public void Run()
        {
            MyIDelegate dg = MyIFunc;
            DoSomething(MyIFunc);

        }
    }
}
