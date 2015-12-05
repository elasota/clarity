namespace System
{
    using System.Runtime.CompilerServices;

    [Clarity.ExportStub("System_Activator.cpp")]
    public sealed class Activator
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern static T CreateInstance<T>();
    }
}
