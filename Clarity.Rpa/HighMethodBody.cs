using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighMethodBody
    {
        private HighRegion m_region;
        private HighLocal[] m_args;
        private HighLocal[] m_locals;
        private bool m_haveDebugInfo;

        public HighRegion MainRegion { get { return m_region; } }
        public HighLocal[] Args { get { return m_args; } }
        public HighLocal[] Locals { get { return m_locals; } }
        public bool HaveDebugInfo { get { return m_haveDebugInfo; } }

        public HighMethodBody(HighRegion region, HighLocal[] args, HighLocal[] locals, bool haveDebugInfo)
        {
            m_region = region;
            m_args = args;
            m_locals = locals;
            m_haveDebugInfo = haveDebugInfo;
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write((uint)m_args.Length);
            writer.Write((uint)m_locals.Length);
            writer.Write(m_haveDebugInfo);

            List<HighLocal> allLocals = new List<HighLocal>();
            allLocals.AddRange(m_args);
            allLocals.AddRange(m_locals);

            foreach (HighLocal local in allLocals)
                local.Write(fileBuilder, writer);

            HighMethodBuilder methodBuilder = new HighMethodBuilder(allLocals.ToArray());

            m_region.Write(fileBuilder, methodBuilder, m_haveDebugInfo, writer);
        }

        public static HighMethodBody Read(TagRepository rpa, CatalogReader catalog, MethodDeclTag methodDecl, BinaryReader reader)
        {
            uint numArgs = reader.ReadUInt32();
            uint numLocals = reader.ReadUInt32();
            bool haveDebugInfo = reader.ReadBoolean();

            HighLocal[] args = new HighLocal[numArgs];
            HighLocal[] locals = new HighLocal[numLocals];

            for (uint i = 0; i < numArgs; i++)
            {
                HighLocal arg = new HighLocal();
                arg.Read(rpa, catalog, reader);
                args[i] = arg;
            }

            for (uint i = 0; i < numLocals; i++)
            {
                HighLocal local = new HighLocal();
                local.Read(rpa, catalog, reader);
                locals[i] = local;
            }

            HighMethodBodyParseContext parseContext = new HighMethodBodyParseContext(args, locals);

            CodeLocationTag baseLocation = new CodeLocationTag(methodDecl, 0);

            HighRegion region = HighRegion.Read(rpa, catalog, parseContext, baseLocation, haveDebugInfo, reader);

            return new HighMethodBody(region, args, locals, haveDebugInfo);
        }
    }
}
