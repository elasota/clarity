using System.Collections.Generic;

namespace AssemblyImporter.CLR.CIL
{
    public abstract class MethodDataSection
    {
        // II.25.4.5
        public static MethodDataSection Parse(CLRMetaDataParser parser, Dictionary<uint, uint> offsetToInstruction, out bool moreSections)
        {
            byte sectionHeader = parser.ReadU8();

            moreSections = ((sectionHeader & 0x80) != 0);
            bool isFat = ((sectionHeader & 0x40) != 0);

            uint sectionKind = ((uint)sectionHeader & 0x3f);

            if (sectionKind == 0x1)
                return new MethodEHSection(parser, offsetToInstruction, isFat);
            else
                throw new ParseFailedException("Unknown method section type");
        }
    }
}
