using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.PE
{
    public class PEAssembly
    {
        public PESectionHeader[] SectionHeaders { get; private set; }
        public RvaAndSize[] DataDirectory { get; private set; }

        public PEAssembly(StreamParser parser)
        {
            DOSHeader dosHeader = new DOSHeader(parser);
            parser.Seek(dosHeader.PEHeaderOffset);
            PEHeader peHeader = new PEHeader(parser);
            if (peHeader.SizeOfOptionalHeader == 0)
                throw new ParseFailedException("PE missing NT header");
            PEOptionalHeader peOptionalHeader = new PEOptionalHeader(parser);
            PESectionHeader[] sectionHeaders = new PESectionHeader[peHeader.NumberOfSections];
            for (int i = 0; i < peHeader.NumberOfSections; i++)
                sectionHeaders[i] = new PESectionHeader(parser);

            SectionHeaders = sectionHeaders;
            DataDirectory = peOptionalHeader.DataDirectory;
        }

        public long ResolveRva(uint rva)
        {
            foreach (PESectionHeader sh in SectionHeaders)
            {
                if (rva >= sh.VirtualAddress && rva - sh.VirtualAddress < sh.SizeOfRawData)
                    return (long)rva - sh.VirtualAddress + sh.PointerToRawData;
            }
            throw new ParseFailedException("Loader could not resolve RVA");
        }
    }
}
