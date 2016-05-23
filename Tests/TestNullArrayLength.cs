using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestNullArrayLength
    {
        public void DoNotRun()
        {
            int src = ((int[])null).Length;
        }
    }
}
