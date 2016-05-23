using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public class HighInterfaceImplementation
    {
        private TypeSpecClassTag m_type;
        private HighInterfaceMethodImplementation[] m_methodImpls;

        public TypeSpecClassTag Interface { get { return m_type; } }
        public HighInterfaceMethodImplementation[] MethodImpls { get { return m_methodImpls; } }

        public void Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            TypeSpecTag typeTag = catalog.GetTypeSpec(reader.ReadUInt32());
            if (!(typeTag is TypeSpecClassTag))
                throw new Exception("Interface implementation implements a non-interface");
            m_type = (TypeSpecClassTag)typeTag;

            uint numMethods = reader.ReadUInt32();
            List<HighInterfaceMethodImplementation> impls = new List<HighInterfaceMethodImplementation>();

            for (uint i = 0; i < numMethods; i++)
            {
                if (reader.ReadBoolean())   // HACK - FIXME
                {
                    HighInterfaceMethodImplementation impl = new HighInterfaceMethodImplementation();
                    impl.Read(rpa, catalog, reader);
                    impls.Add(impl);
                }
            }

            m_methodImpls = impls.ToArray();
        }
    }
}
