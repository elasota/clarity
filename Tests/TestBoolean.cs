using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestBoolean
    {
        public void SetBool(ref bool target, bool value)
        {
            if (target != value)
                target = value;
        }
    }
}
