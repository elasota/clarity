using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clarity.Rpa
{
    public class MethodSignatureTag : IEquatable<MethodSignatureTag>, IInternable
    {
        public bool HasThis { get; private set; }
        public bool ExplicitThis { get; private set; }
        public uint NumGenericParameters { get; private set; }
        public TypeSpecTag RetType { get; private set; }
        public MethodSignatureParam[] ParamTypes { get; private set; }
        public bool IsInterned { get; set; }

        public MethodSignatureTag(bool hasThis, bool explicitThis, uint numGenericParameters, TypeSpecTag retType, MethodSignatureParam[] paramTypes)
        {
            HasThis = hasThis;
            ExplicitThis = explicitThis;
            NumGenericParameters = numGenericParameters;
            RetType = retType;
            ParamTypes = paramTypes;
        }

        public bool Equals(MethodSignatureTag other)
        {
            if (this.IsInterned && other.IsInterned)
                return this == other;

            if (HasThis != other.HasThis)
                return false;
            if (ExplicitThis != other.ExplicitThis)
                return false;
            if (NumGenericParameters != other.NumGenericParameters)
                return false;
            if (RetType != other.RetType)
                return false;
            if (ParamTypes.Length != other.ParamTypes.Length)
                return false;

            for (int i = 0; i < ParamTypes.Length; i++)
                if (!ParamTypes[i].Equals(other.ParamTypes[i]))
                    return false;
            return true;
        }

        public static MethodSignatureTag Read(CatalogReader rpa, BinaryReader reader)
        {
            bool hasThis = reader.ReadBoolean();
            bool explicitThis = reader.ReadBoolean();
            uint numGenericParameters = reader.ReadUInt32();
            TypeSpecTag returnType = rpa.GetTypeSpec(reader.ReadUInt32());
            uint numParamTypes = reader.ReadUInt32();

            MethodSignatureParam[] paramTypes = new MethodSignatureParam[numParamTypes];

            for (uint i = 0; i < numParamTypes; i++)
                paramTypes[i] = MethodSignatureParam.Read(rpa, reader);

            return new MethodSignatureTag(hasThis, explicitThis, numGenericParameters, returnType, paramTypes);
        }

        public MethodSignatureTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecTag newRetType = this.RetType.Instantiate(repo, argTypes);

            List<MethodSignatureParam> newParamTypes = new List<MethodSignatureParam>();
            foreach (MethodSignatureParam paramTag in this.ParamTypes)
                newParamTypes.Add(paramTag.Instantiate(repo, argTypes));

            MethodSignatureTag newSignature = new MethodSignatureTag(this.HasThis, this.ExplicitThis, this.NumGenericParameters, newRetType, newParamTypes.ToArray());
            newSignature = repo.InternMethodSignature(newSignature);

            return newSignature;
        }

        public void Write(StreamWriter writer)
        {
            writer.Write("msig ( ");
            writer.Write(HasThis ? "true, " : "false, ");
            writer.Write(ExplicitThis ? "true, " : "false, ");
            writer.Write(NumGenericParameters);
            writer.Write(", ");
            RetType.Write(writer);
            writer.Write(", ( ");
            for (int i = 0; i < ParamTypes.Length; i++)
            {
                if (i != 0)
                    writer.Write(", ");
                ParamTypes[i].Write(writer);
            }
            writer.Write(") ) ");
        }

        public override bool Equals(object other)
        {
            MethodSignatureTag tOther = other as MethodSignatureTag;

            if (tOther == null)
                return false;

            return this.Equals(tOther);
        }

        public override int GetHashCode()
        {
            int hash = 0;
            hash += HasThis.GetHashCode();
            hash += ExplicitThis.GetHashCode();
            hash += NumGenericParameters.GetHashCode();
            hash += RetType.GetHashCode();
            hash += ParamTypes.Length.GetHashCode();

            foreach (MethodSignatureParam paramType in ParamTypes)
                hash += paramType.GetHashCode();
            return hash;
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write(HasThis);
            writer.Write(ExplicitThis);
            writer.Write(NumGenericParameters);
            writer.Write(fileBuilder.IndexTypeSpecTag(RetType));
            writer.Write((uint)ParamTypes.Length);

            foreach (MethodSignatureParam paramTag in ParamTypes)
                paramTag.Write(fileBuilder, writer);
        }
    }
}
