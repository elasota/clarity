using System.Runtime.CompilerServices;

namespace System.Runtime.Remoting
{
    [Clarity.ExportStub("System_Runtime_Remoting_RemotingServices.cpp")]
    public static class RemotingServices
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern bool IsTransparentProxy(Object proxy);
    }
}


