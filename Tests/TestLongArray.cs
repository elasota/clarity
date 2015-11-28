using System;

namespace Tests
{
    public class TestLongArray
    {
        public int[] CreateLongArray(long sz)
        {
            return new int[sz];
        }

        public void Run()
        {
            CreateLongArray(4);
        }
    }
}
