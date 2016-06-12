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

        public HighSsaRegister Dest { get { return m_dest; } }
        public TypeSpecTag StaticType { get { return m_staticType; } }
        public string FieldName { get { return m_fieldName; } }

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

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            m_staticType.WriteDisassembly(dw);
            dw.Write(" ");
            dw.WriteToken(m_fieldName);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_staticType = catalog.GetTypeSpec(reader.ReadUInt32());
            m_fieldName = catalog.GetString(reader.ReadUInt32());
        }

        protected override HighInstruction CloneImpl()
        {
            return new GetStaticFieldAddrInstruction(CodeLocation, m_dest, m_staticType, m_fieldName);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_staticType);
        }

        public override bool MayThrow { get { return true; } }
    }
}
