using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    [Clarity.ExportStub("System_Diagnostics_Debugger.cpp")]
    public static class Debugger
    {
        public static extern bool IsAttached
        {
            [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get;
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void Break();
    }
}


