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
        private uint m_numGenericParameters;
        private TypeSpecTag m_retType;
        private MethodSignatureParam[] m_paramTypes;

        public uint NumGenericParameters { get { return m_numGenericParameters; } }
        public TypeSpecTag RetType { get { return m_retType; } }
        public MethodSignatureParam[] ParamTypes { get { return m_paramTypes; } }
        public bool IsInterned { get; set; }

        public MethodSignatureTag(uint numGenericParameters, TypeSpecTag retType, MethodSignatureParam[] paramTypes)
        {
            m_numGenericParameters = numGenericParameters;
            m_retType = retType;
            m_paramTypes = paramTypes;
        }

        public bool Equals(MethodSignatureTag other)
        {
            if (this.IsInterned && other.IsInterned)
                return this == other;

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
            uint numGenericParameters = reader.ReadUInt32();
            TypeSpecTag returnType = rpa.GetTypeSpec(reader.ReadUInt32());
            uint numParamTypes = reader.ReadUInt32();

            MethodSignatureParam[] paramTypes = new MethodSignatureParam[numParamTypes];

            for (uint i = 0; i < numParamTypes; i++)
                paramTypes[i] = MethodSignatureParam.Read(rpa, reader);

            return new MethodSignatureTag(numGenericParameters, returnType, paramTypes);
        }

        public MethodSignatureTag Instantiate(TagRepository repo, TypeSpecTag[] argTypes)
        {
            TypeSpecTag newRetType = this.RetType.Instantiate(repo, argTypes);

            List<MethodSignatureParam> newParamTypes = new List<MethodSignatureParam>();
            foreach (MethodSignatureParam paramTag in this.ParamTypes)
                newParamTypes.Add(paramTag.Instantiate(repo, argTypes));

            MethodSignatureTag newSignature = new MethodSignatureTag(m_numGenericParameters, newRetType, newParamTypes.ToArray());
            newSignature = repo.InternMethodSignature(newSignature);

            return newSignature;
        }

        public MethodSignatureTag Instantiate(TagRepository repo, TypeSpecTag[] typeParams, TypeSpecTag[] methodParams)
        {
            TypeSpecTag newRetType = this.RetType.Instantiate(repo, typeParams, methodParams);

            List<MethodSignatureParam> newParamTypes = new List<MethodSignatureParam>();
            foreach (MethodSignatureParam paramTag in this.ParamTypes)
                newParamTypes.Add(paramTag.Instantiate(repo, typeParams, methodParams));

            MethodSignatureTag newSignature = new MethodSignatureTag(m_numGenericParameters, newRetType, newParamTypes.ToArray());
            newSignature = repo.InternMethodSignature(newSignature);

            return newSignature;
        }

        public void Write(StreamWriter writer)
        {
            writer.Write("msig ( ");
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

        public void WriteDisassembly(DisassemblyWriter dw)
        {
            dw.Write("msig(");
            m_retType.WriteDisassembly(dw);
            dw.Write(",(");
            for (int i = 0; i < m_paramTypes.Length; i++)
            {
                if (i != 0)
                    dw.Write(",");
                m_paramTypes[i].WriteDisassembly(dw);
            }
            dw.Write("))");
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
            hash += NumGenericParameters.GetHashCode();
            hash += RetType.GetHashCode();
            hash += ParamTypes.Length.GetHashCode();

            foreach (MethodSignatureParam paramType in ParamTypes)
                hash += paramType.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            string result = m_retType.ToString() + ":";

            if (m_numGenericParameters > 0)
                result += "<#" + m_numGenericParameters.ToString() + ">";
            result += "(";
            for (int i = 0; i < m_paramTypes.Length; i++)
            {
                if (i != 0)
                    result += ",";

                result += m_paramTypes[i].ToString();
            }
            result += ")";
            return result;
        }

        public void Write(HighFileBuilder fileBuilder, BinaryWriter writer)
        {
            writer.Write(m_numGenericParameters);
            writer.Write(fileBuilder.IndexTypeSpecTag(m_retType));
            writer.Write((uint)m_paramTypes.Length);

            foreach (MethodSignatureParam paramTag in ParamTypes)
                paramTag.Write(fileBuilder, writer);
        }
    }
}
