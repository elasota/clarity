using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler.Instructions
{
    public sealed class GetRloFieldInfoInstruction : Rpa.HighInstruction, Rpa.ITypeReferencingInstruction
    {
        private HighSsaRegister m_dest;
        private TypeSpecTag m_type;
        private uint m_fieldIndex;
        private bool m_isStatic;

        public override Opcodes Opcode { get { return Opcodes.GetRloFieldInfo; } }

        public GetRloFieldInfoInstruction(CodeLocationTag codeLocation, HighSsaRegister dest, TypeSpecTag type, uint fieldIndex, bool isStatic)
            : base(codeLocation)
        {
            m_dest = dest;
            m_type = type;
            m_fieldIndex = fieldIndex;
            m_isStatic = isStatic;
        }

        public override HighInstruction Clone()
        {
            return new GetRloFieldInfoInstruction(this.CodeLocation, m_dest, m_type, m_fieldIndex, m_isStatic);
        }

        public override void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            m_fieldIndex = reader.ReadUInt32();
            m_isStatic = reader.ReadBoolean();
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
            writer.Write(m_fieldIndex);
            writer.Write(m_isStatic);
        }

        void ITypeReferencingInstruction.VisitTypes(VisitTypeSpecDelegate visitor)
        {
            visitor(ref m_type);
        }

        public override bool MayThrow { get { return false; } }
    }
}
