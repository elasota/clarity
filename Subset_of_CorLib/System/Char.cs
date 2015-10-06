////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace System
{

    using System;
    using System.Runtime.CompilerServices;

    [Serializable]
    public struct Char
    {
        //
        // Public Constants
        //
        /**
         * The maximum character value.
         */
        public const char MaxValue = (char)0xFFFF;
        /**
         * The minimum character value.
         */
        public const char MinValue = (char)0x00;

        public override String ToString()
        {
            return new String(this, 1);
        }

        public char ToLower()
        {
            if ('A' <= this && this <= 'Z')
            {
                return (char)(this - ('A' - 'a'));
            }

            return this;
        }

        public char ToUpper()
        {
            if ('a' <= this && this <= 'z')
            {
                return (char)(this + ('A' - 'a'));
            }

            return this;
        }
    }
}


