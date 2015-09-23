using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public sealed class CppVtableSlot
    {
        public sealed class SlotKey : IEquatable<SlotKey>
        {
            public CLRMethodSignatureInstance Signature { get; private set; }
            public string Name { get; private set; }

            public bool Equals(SlotKey other)
            {
                return Name == other.Name && Signature.Equals(other.Signature);
            }

            public override int GetHashCode()
            {
                return Signature.GetHashCode() + Name.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj != null && (obj is SlotKey) && ((SlotKey)obj).Equals(this);
            }
        }

        public CLRMethodSignatureInstance Signature { get; private set; }
        public CLRMethodSignatureInstance DeclaredSignature { get; private set; }
        public CLRTypeSpec DisambigSpec { get; private set; }
        public string Name { get; private set; }
        public string VtableMangle { get; private set; }
        public string InternalName { get; private set; }
        public bool IsGenericInterface { get; private set; }

        public CppVtableSlot(CLRMethodSignatureInstance sig, CLRTypeSpec disambig, string name, string internalName, string vtableMangle, bool isGenericInterface)
        {
            if (name.Contains("."))
                throw new ArgumentException();
            VtableMangle = vtableMangle;
            Name = name;
            InternalName = internalName;
            Signature = sig;
            DeclaredSignature = sig;
            DisambigSpec = disambig;
            IsGenericInterface = isGenericInterface;
        }

        private CppVtableSlot(CppVtableSlot baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            Signature = baseInstance.Signature.Instantiate(typeParams, methodParams);
            Name = baseInstance.Name;
            VtableMangle = baseInstance.VtableMangle;
            InternalName = baseInstance.InternalName;
            DeclaredSignature = baseInstance.DeclaredSignature;
            DisambigSpec = baseInstance.DisambigSpec.Instantiate(typeParams, methodParams);
            IsGenericInterface = baseInstance.IsGenericInterface;
        }

        public CppVtableSlot Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CppVtableSlot(this, typeParams, methodParams);
        }

        public string GenerateName()
        {
            return "vs" + Name + VtableMangle;
        }
    }
}
