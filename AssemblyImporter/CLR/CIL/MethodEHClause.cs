using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR.CIL
{
    // II.25.4.6
    public class MethodEHClause
    {
        public enum ExceptionClauseType
        {
            Exception = 0x0000,
            Filter = 0x0001,
            Finally = 0x0002,
            Fault = 0x0004,
        }

        public uint TryOffset { get; private set; }
        public uint TryLength { get; private set; }
        public uint HandlerOffset { get; private set; }
        public uint HandlerLength { get; private set; }
        public uint FilterOffset { get; private set; }
        public CLRTableRow ClassToken { get; private set; }

        public ExceptionClauseType ClauseType { get; private set; }

        public MethodEHClause(CLRMetaDataParser parser, Dictionary<uint, uint> offsetToInstruction, bool isFat)
        {
            uint flags;

            if (!isFat)
            {
                flags = parser.ReadU16();
                TryOffset = parser.ReadU16();
                TryLength = parser.ReadU8();
                HandlerOffset = parser.ReadU16();
                HandlerLength = parser.ReadU8();
            }
            else
            {
                flags = parser.ReadU32();
                TryOffset = parser.ReadU32();
                TryLength = parser.ReadU32();
                HandlerOffset = parser.ReadU32();
                HandlerLength = parser.ReadU32();
            }

            ClauseType = (ExceptionClauseType)flags;

            if (ClauseType != ExceptionClauseType.Exception
                && ClauseType != ExceptionClauseType.Filter
                && ClauseType != ExceptionClauseType.Finally
                && ClauseType != ExceptionClauseType.Fault)
                throw new ParseFailedException("Unknown exception type");

            if (ClauseType == ExceptionClauseType.Filter)
                FilterOffset = parser.ReadU32();
            else if (ClauseType == ExceptionClauseType.Exception)
                ClassToken = parser.ReadFatToken();
            else
                parser.ReadU32();

            // Unpatch
            uint tryStart = offsetToInstruction[TryOffset];
            uint tryEnd = offsetToInstruction[TryOffset + TryLength];
            uint handlerStart = offsetToInstruction[HandlerOffset];
            uint handlerEnd = offsetToInstruction[HandlerOffset + HandlerLength];
            uint filterInstr = offsetToInstruction[FilterOffset];

            TryOffset = tryStart;
            TryLength = tryEnd - tryStart;
            HandlerOffset = handlerStart;
            HandlerLength = handlerEnd - handlerStart;
            FilterOffset = filterInstr;

        }
    }
}
