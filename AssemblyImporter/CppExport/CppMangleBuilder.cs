using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppMangleBuilder
    {
        private List<byte> m_bytes;

        public CppMangleBuilder()
        {
            m_bytes = new List<byte>();
        }

        public void AddBytes(IEnumerable<byte> bytes)
        {
            m_bytes.AddRange(bytes);
        }

        public void Cycle()
        {
            byte[] hash = SHA256Managed.Create().ComputeHash(m_bytes.ToArray());
            m_bytes.Clear();
            m_bytes.AddRange(hash);
        }

        public byte[] FinishAsBytes()
        {
            return SHA256Managed.Create().ComputeHash(m_bytes.ToArray());
        }

        public string Finish()
        {
            byte[] hash = FinishAsBytes();

            string fragments = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234";
            ulong largeCode = 0;
            for (int i = 0; i < 8; i++)
                largeCode |= ((ulong)hash[i]) << i * 8;
            string result = "";
            for (int i = 0; i < 11; i++)
            {
                int subset = (int)(largeCode % 57);
                largeCode /= 57;
                result += fragments[subset];
            }
            return result;
        }

        public void Add(string str)
        {
            CppMangleBuilder builder = new CppMangleBuilder();
            builder.AddBytes(System.Text.Encoding.UTF8.GetBytes(str));
            AddBytes(builder.FinishAsBytes());
        }

        public void Add(CLRTypeDefRow typeDef)
        {
            CppMangleBuilder builder = new CppMangleBuilder();
            if (typeDef.ContainerClass != null)
            {
                builder.Add(typeDef.ContainerClass);
                builder.Cycle();
            }
            builder.Add(typeDef.TypeNamespace);
            builder.Cycle();
            builder.Add(typeDef.TypeName);
            AddBytes(builder.FinishAsBytes());
        }

        public void Add(CLRMethodSignatureInstance methodSig)
        {
            CppMangleBuilder builder = new CppMangleBuilder();
            builder.Add(methodSig.RetType);
            builder.Cycle();
            foreach (CLRMethodSignatureInstanceParam param in methodSig.ParamTypes)
            {
                builder.Add((int)param.TypeOfType);
                builder.Add(param.Type);
                builder.Cycle();
            }
            AddBytes(builder.FinishAsBytes());
        }

        public void Add(CLRTypeSpec typeSpec)
        {
            CppMangleBuilder builder = new CppMangleBuilder();
            if (typeSpec is CLRTypeSpecClass)
            {
                CLRTypeSpecClass cls = (CLRTypeSpecClass)typeSpec;
                builder.Add("class");
                builder.Add(cls.TypeDef);
            }
            else if (typeSpec is CLRTypeSpecGenericInstantiation)
            {
                CLRTypeSpecGenericInstantiation gi = (CLRTypeSpecGenericInstantiation)typeSpec;
                builder.Add("gi");
                builder.Add(gi.GenericType);
                builder.Add(gi.ArgTypes.Length);
                foreach (CLRTypeSpec argType in gi.ArgTypes)
                    builder.Add(argType);
            }
            else if (typeSpec is CLRTypeSpecSimple)
            {
                builder.Add("simple");
                builder.Add((int)((CLRTypeSpecSimple)typeSpec).BasicType);
            }
            else if (typeSpec is CLRTypeSpecSZArray)
            {
                builder.Add("szarray");
                builder.Add(((CLRTypeSpecSZArray)typeSpec).SubType);
            }
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                CLRTypeSpecVarOrMVar varOrMVar = (CLRTypeSpecVarOrMVar)typeSpec;
                builder.Add("varOrMVar");
                builder.Add((int)varOrMVar.ElementType);
                builder.Add((int)varOrMVar.Value);
            }
            else
                throw new ParseFailedException("Strange type spec");
            AddBytes(builder.FinishAsBytes());
        }

        public void Add(int v)
        {
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = (byte)(v & 0xff);
                v >>= 8;
            }
            AddBytes(bytes);
        }
    }
}
