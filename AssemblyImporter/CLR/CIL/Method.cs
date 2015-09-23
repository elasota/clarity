using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR.CIL
{
    // II.25.4.1
    public class Method
    {
        private const uint CorILMethod_TinyFormat = 0x2;
        private const uint CorILMethod_FatFormat = 0x3;
        private const uint CorILMethod_MoreSects = 0x8;
        private const uint CorILMethod_InitLocals = 0x10;

        public uint MaxStack { get; private set; }
        public CLRSigLocalVarSig LocalVarSig { get; private set; }
        public MethodDataSection[] Sections { get; private set; }
        public HLInstruction[] Instructions { get; private set; }

        private Dictionary<uint, uint> m_offsetToInstruction;
        private List<uint> m_instructionToOffset;

        public Method(CLRMetaDataParser parser)
        {
            m_offsetToInstruction = new Dictionary<uint, uint>();
            m_instructionToOffset = new List<uint>();

            byte headByte = parser.ReadU8();
            uint codeSize;
            bool haveSections;

            int format = (headByte & 0x3);
            if (format == CorILMethod_TinyFormat)
            {
                // Tiny format (II.25.4.2)
                codeSize = ((uint)headByte >> 2);

                MaxStack = 8;
                haveSections = false;
            }
            else if (format == CorILMethod_FatFormat)
            {
                // Fat format (II.25.4.3)
                byte highByte = parser.ReadU8();
                uint flagsAndSize = ((uint)headByte | ((uint)highByte << 8));

                uint headerSizeInDWords = (flagsAndSize >> 12) & 0xf;

                if (headerSizeInDWords != 3)
                    throw new ParseFailedException("Unsupported method format");
                MaxStack = parser.ReadU16();
                codeSize = parser.ReadU32();
                CLRStandAloneSigRow localSig = (CLRStandAloneSigRow)parser.ReadFatToken();
                if (localSig != null)
                    LocalVarSig = new CLRSigLocalVarSig(new CLRSignatureParser(localSig.Signature, parser.Tables));

                haveSections = ((flagsAndSize & CorILMethod_MoreSects) != 0);
            }
            else
                throw new ParseFailedException("Invalid IL method format");

            ParseCode(parser, codeSize);

            List<MethodDataSection> sections = new List<MethodDataSection>();
            while (haveSections)
            {
                parser.Align(4);
                sections.Add(MethodDataSection.Parse(parser, m_offsetToInstruction, out haveSections));
            }
            Sections = sections.ToArray();
        }

        public void ParseCode(CLRMetaDataParser parser, uint codeSize)
        {
            InstructionParser instParser = new InstructionParser(parser.ReadBytes(codeSize), parser.Tables, parser.BinData);

            List<HLInstruction> instructions = new List<HLInstruction>();
            while (!instParser.AtEnd)
            {
                long pos = instParser.Position;
                m_offsetToInstruction[(uint)pos] = (uint)instructions.Count;
                m_instructionToOffset.Add((uint)pos);
                instructions.Add(instParser.DecodeInstruction());
            }
            m_instructionToOffset.Add((uint)instParser.Position);

            // Unpatch instructions
            for (int i = 0; i < instructions.Count; i++)
            {
                HLInstruction instr = instructions[i];
                if ((instr.Flags & HLOpFlags.Br) != 0)
                {
                    instr.ConvertBranchTargets(m_instructionToOffset[i + 1], m_offsetToInstruction);
                    instructions[i] = instr;
                }
            }

            Instructions = instructions.ToArray();
        }
    }
}
