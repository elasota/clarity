using System;

namespace Tests
{
    public class TestExceptionHandling
    {
        public class MyException : Exception
        {
        }

        private void ThrowSomething()
        {
            throw new MyException();
        }

        public void Run()
        {
            try
            {
                try
                {
                    ThrowSomething();
                }
                catch(MyException mex)
                {
                    TestApi.WriteLine("Ran mex handler 1");
                }
                catch(Exception ex2)
                {
                    TestApi.WriteLine("Ran wrong ex handler");
                }
                finally
                {
                    TestApi.WriteLine("Ran finally handler 1");
                }
            }
            catch(MyException mex)
            {
                TestApi.WriteLine("Ran mex handler 2");
            }
            finally
            {
                TestApi.WriteLine("Ran finally handler 2");
                try
                {
                    ThrowSomething();
                }
                catch(MyException mex2)
                {
                    TestApi.WriteLine("Caught exception in finally");
                }
                TestApi.WriteLine("Done with finally handler 2");
            }

            TestApi.WriteLine("Finished OK");
        }
    }
}
