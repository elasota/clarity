using System;

namespace AssemblyImporter.TCLR
{
    public enum TCLRCallingConvention
    {
        PIMAGE_CEE_CS_CALLCONV_DEFAULT = 0x0,

        PIMAGE_CEE_CS_CALLCONV_VARARG = 0x5,
        PIMAGE_CEE_CS_CALLCONV_FIELD = 0x6,
        PIMAGE_CEE_CS_CALLCONV_LOCAL_SIG = 0x7,
        PIMAGE_CEE_CS_CALLCONV_PROPERTY = 0x8,
        PIMAGE_CEE_CS_CALLCONV_UNMGD = 0x9,
        PIMAGE_CEE_CS_CALLCONV_GENERICINST = 0xa,  // generic method instantiation
        PIMAGE_CEE_CS_CALLCONV_NATIVEVARARG = 0xb,  // used ONLY for 64bit vararg PInvoke calls
        PIMAGE_CEE_CS_CALLCONV_MAX = 0xc,  // first invalid calling convention

        // The high bits of the calling convention convey additional info
        PIMAGE_CEE_CS_CALLCONV_MASK = 0x0f, // Calling convention is bottom 4 bits
        PIMAGE_CEE_CS_CALLCONV_HASTHIS = 0x20, // Top bit indicates a 'this' parameter
        PIMAGE_CEE_CS_CALLCONV_EXPLICITTHIS = 0x40, // This parameter is explicitly in the signature
        PIMAGE_CEE_CS_CALLCONV_GENERIC = 0x10, // Generic method sig with explicit number of type arguments (precedes ordinary parameter count)
    }
}
