using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    public class TestStaticInitializers
    {
        public class MyNotBFI
        {
            public static void TriggerStaticInit()
            {
                TestApi.WriteLine("Triggered static init");
            }

            static MyNotBFI()
            {
                TestApi.WriteLine("Static ctor");
            }
        }

        public void Run()
        {
            MyNotBFI.TriggerStaticInit();
        }
    }
}
