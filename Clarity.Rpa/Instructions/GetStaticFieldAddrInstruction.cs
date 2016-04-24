using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public sealed class GetStaticFieldAddrInstruction : HighInstruction, ITypeReferencingInstruction
    {
        private HighSsaRegister m_dest;
        private TypeSpecTag m_staticType;
        private string m_fieldName;

        public override Opcodes Opcode { get { return Opcodes.GetStaticFieldAddr; } }

        public GetStaticFieldAddrInstruction()
        {
        }

        public GetStaticFieldAddrInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, TypeSpecTag staticType, string fieldName)
            : base(codeLocation)
        {
            m_dest = dest;
            m_staticType = staticType;
            m_fieldName = fieldName;
        }

        public override void VisitSsaDests(VisitSsaDelegate visitor)
        {
            visitor(ref m_dest);
        }

        public override void VisitSsaUses(VisitSsaDelegate visitor)
        {
        }

        public override void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write(fileBuilder.IndexTypeSpecTag(m_staticType));
            writer.Write(fileBuilder.IndexString(m_fieldName));
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_staticType = catalog.GetTypeSpec(reader.ReadUInt32());
            m_fieldName = catalog.GetString(reader.ReadUInt32());
        }

        public override HighInstruction Clone()
        {
            return new GetStaticFieldAddrInstruction(CodeLocation, m_dest, m_staticType, m_fieldName);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_staticType);
        }
    }
}
