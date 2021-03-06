////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Runtime.CompilerServices;

namespace System.Text
{
    [Clarity.ExportStub("System_Text_UTF8Decoder.cpp")]
    internal class UTF8Decoder : Decoder
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public override extern void Convert(byte[] bytes, int byteIndex, int byteCount,
            char[] chars, int charIndex, int charCount, bool flush,
            out int bytesUsed, out int charsUsed, out bool completed);
    }
}


