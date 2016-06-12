using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa.Instructions
{
    public class GetFieldInfoInstruction : HighInstruction, ITypeReferencingInstruction
    {
        private HighSsaRegister m_dest;
        private TypeSpecTag m_type;
        private string m_fieldName;
        private bool m_isStatic;

        public HighSsaRegister Dest { get { return m_dest; } }
        public TypeSpecTag Type { get { return m_type; } }
        public string FieldName { get { return m_fieldName; } }
        public bool IsStatic { get { return m_isStatic; } }

        public override Opcodes Opcode { get { return Opcodes.GetFieldInfo; } }

        public GetFieldInfoInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, TypeSpecTag type, string fieldName, bool isStatic)
            : base(codeLocation)
        {
            m_dest = dest;
            m_type = type;
            m_fieldName = fieldName;
            m_isStatic = isStatic;
        }

        public GetFieldInfoInstruction()
        {
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
            writer.Write(fileBuilder.IndexTypeSpecTag(m_type));
            writer.Write(fileBuilder.IndexString(m_fieldName));
            writer.Write(m_isStatic);
        }

        protected override void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw)
        {
            m_type.WriteDisassembly(dw);
            dw.Write(" ");
            dw.WriteToken(m_fieldName);
            dw.Write(" ");
            dw.Write(m_isStatic.ToString());
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_type = catalog.GetTypeSpec(reader.ReadUInt32());
            m_fieldName = catalog.GetString(reader.ReadUInt32());
            m_isStatic = reader.ReadBoolean();
        }

        protected override HighInstruction CloneImpl()
        {
            return new GetFieldInfoInstruction(CodeLocation, m_dest, m_type, m_fieldName, m_isStatic);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }

        public override bool MayThrow { get { return false; } }
    }
}
