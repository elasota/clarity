using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighInterfaceMethodImplementation
    {
        private MethodDeclTag m_ifcSlot;
        private MethodDeclTag m_classSlot;

        public MethodDeclTag InterfaceSlot { get { return m_ifcSlot; } }
        public MethodDeclTag ClassSlot { get { return m_classSlot; } }

        public void Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader)
        {
            m_ifcSlot = catalog.GetMethodDecl(reader.ReadUInt32());
            m_classSlot = catalog.GetMethodDecl(reader.ReadUInt32());
        }
    }
}
