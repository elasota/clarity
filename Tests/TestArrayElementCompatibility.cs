using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    // I.8.7.1
    public class TestArrayElementCompatibility
    {
        public IList<ushort> Test(short[] v)
        {
            return (IList<ushort>)(object)v;
        }

        public void Run()
        {
            IList<ushort> v = Test(new short[4]);
        }
    }
}
