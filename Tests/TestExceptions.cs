using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestExceptions
    {
        public class MyException : Exception
        {
        }

        private static void ThrowAnException()
        {
            throw new MyException();
        }

        private static bool BranchFunc()
        {
            return true;
        }

        public void Run()
        {
            TestApi.WriteLine("Running TestExceptions");
            try
            {
                try
                {
                    ThrowAnException();
                }
                catch (System.OutOfMemoryException)
                {
                    if (BranchFunc())
                        TestApi.WriteLine("Branch A");
                    else
                        TestApi.WriteLine("Branch B");
                }
                catch (MyException)
                {
                    TestApi.WriteLine("Running MyException handler");
                }
                finally
                {
                    TestApi.WriteLine("Running finally handler");
                }
            }
            finally
            {
                TestApi.WriteLine("Running second finally handler");
            }
        }
    }
}
