using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public sealed class CppVtableSlot : IEquatable<CppVtableSlot>
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
        public Clarity.Rpa.MethodDeclTag VtableSlotTag { get; private set; }
        public bool IsGenericInterface { get; private set; }
        public bool IsPublic { get; private set; }

        public CppVtableSlot(CLRMethodSignatureInstance sig, CLRTypeSpec disambig, string name, Clarity.Rpa.MethodDeclTag vtableSlotTag, bool isGenericInterface, CLRMethodDefRow rootMethodDef)
        {
            if (vtableSlotTag == null)
                throw new ArgumentException();
            VtableSlotTag = vtableSlotTag;
            Name = name;
            Signature = sig;
            DeclaredSignature = sig;
            DisambigSpec = disambig;
            IsGenericInterface = isGenericInterface;
            IsPublic = (rootMethodDef.MemberAccess == CLRMethodDefRow.MethodMemberAccess.Public);
        }

        private CppVtableSlot(CppVtableSlot baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            Signature = baseInstance.Signature.Instantiate(typeParams, methodParams);
            Name = baseInstance.Name;
            VtableSlotTag = baseInstance.VtableSlotTag;
            DeclaredSignature = baseInstance.DeclaredSignature;
            DisambigSpec = baseInstance.DisambigSpec.Instantiate(typeParams, methodParams);
            IsGenericInterface = baseInstance.IsGenericInterface;
            IsPublic = baseInstance.IsPublic;
        }

        public CppVtableSlot Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CppVtableSlot(this, typeParams, methodParams);
        }

        public void Write(System.IO.StreamWriter writer)
        {
            writer.Write("slot ( ");
            writer.Write(RpaTagFactory.CreateName(Name));
            writer.Write(", ");
            VtableSlotTag.Write(writer);
            writer.Write(") ");
        }

        public override bool Equals(object obj)
        {
            return obj != null && (obj is CppVtableSlot) && this.Equals((CppVtableSlot)obj);
        }

        public override int GetHashCode()
        {
            // TODO?
            return Name.GetHashCode();
        }

        public bool Equals(CppVtableSlot other)
        {
            return Signature.Equals(other.Signature) &&
                DeclaredSignature.Equals(other.DeclaredSignature) &&
                DisambigSpec.Equals(other.DisambigSpec) &&
                Name == other.Name &&
                VtableSlotTag.Equals(other.VtableSlotTag) &&
                IsGenericInterface == other.IsGenericInterface;
        }
    }
}
