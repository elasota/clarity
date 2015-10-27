namespace System
{
    using System.Runtime.CompilerServices;

    public class Activator
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public extern static T CreateInstance<T>();
    }
}
