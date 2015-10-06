using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestApi
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public extern static void WriteLine(string s);
    }
}
