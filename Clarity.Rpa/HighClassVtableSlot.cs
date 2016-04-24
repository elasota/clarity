using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighClassVtableSlot
    {
        private MethodDeclTag m_slotTag;
        private MethodSignatureTag m_methodSignature;
        private MethodDeclTag m_implementingMethodTag;
        private bool m_isAbstract;
        private bool m_isFinal;

        public MethodDeclTag SlotTag { get { return m_slotTag; } }
        public MethodSignatureTag Signature { get { return m_methodSignature; } }
        public MethodDeclTag ImplementingMethodTag { get { return m_implementingMethodTag; } }
        public bool IsAbstract { get { return m_isAbstract; } }
        public bool IsFinal { get { return m_isFinal; } }

        public HighClassVtableSlot(MethodDeclTag slotTag, MethodSignatureTag methodSignature, MethodDeclTag implementingMethodTag, bool isAbstract, bool isFinal)
        {
            m_slotTag = slotTag;
            m_methodSignature = methodSignature;
            m_implementingMethodTag = implementingMethodTag;
            m_isAbstract = isAbstract;
            m_isFinal = isFinal;
        }

        public static HighClassVtableSlot Read(TagRepository rpa, CatalogReader catalog, BinaryReader reader, bool isInterface)
        {
            MethodDeclTag slotTag = catalog.GetMethodDecl(reader.ReadUInt32());
            MethodSignatureTag methodSignature = catalog.GetMethodSignature(reader.ReadUInt32());
            bool isAbstract;
            bool isFinal;
            MethodDeclTag implementingMethodTag = null;

            if (isInterface)
            {
                isAbstract = true;
                isFinal = false;
            }
            else
            {
                isAbstract = reader.ReadBoolean();
                if (!isAbstract)
                {
                    if (methodSignature.NumGenericParameters > 0)
                        isFinal = true;
                    else
                        isFinal = reader.ReadBoolean();
                    implementingMethodTag = catalog.GetMethodDecl(reader.ReadUInt32());
                }
                else
                    isFinal = false;
            }

            return new HighClassVtableSlot(slotTag, methodSignature, implementingMethodTag, isAbstract, isFinal);
        }
    }
}
