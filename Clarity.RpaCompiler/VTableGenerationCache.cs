using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class VTableGenerationCache
    {
        public enum PodFlags
        {
            None = 0,
            Equality = 1,
            HashCode = 2,
        }

        public IList<MethodSpecTag> ValueTypeMethodSpecs { get; set; }

        private TypeSpecClassTag m_systemObjectType;
        private TypeSpecClassTag m_systemBoolType;
        private TypeSpecClassTag m_systemTypeType;
        private TypeSpecClassTag m_systemInt32Type;
        private TypeSpecClassTag m_systemUIntPtrType;
        private TypeSpecClassTag m_systemDelegateType;
        private TypeSpecClassTag m_clarityToolsType;
        private MethodDeclTag m_fieldsEqualMethodDecl;
        private MethodDeclTag m_hashBytesMethodDecl;
        private MethodHandle m_objectGetTypeHandle;
        private uint? m_getHashCodeVTableSlot;
        private uint? m_equalsVTableSlot;

        public TypeSpecClassTag GetSystemObjectType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemObjectType, "System", "Object");
        }

        public TypeSpecClassTag GetSystemBoolType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemBoolType, "System", "Boolean");
        }

        public TypeSpecClassTag GetSystemTypeType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemTypeType, "System", "Type");
        }

        public TypeSpecClassTag GetSystemInt32Type(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemInt32Type, "System", "Int32");
        }

        public TypeSpecClassTag GetSystemUIntPtrType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemUIntPtrType, "System", "UIntPtr");
        }

        internal TypeSpecTag GetSystemDelegateType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_systemDelegateType, "System", "Delegate");
        }

        public TypeSpecClassTag GetClarityToolsType(Compiler compiler)
        {
            return GetCachedClass(compiler, ref m_clarityToolsType, "Clarity", "Tools");
        }

        private static TypeSpecClassTag GetCachedClass(Compiler compiler, ref TypeSpecClassTag clsTagRef, string typeNamespace, string typeName)
        {
            TypeSpecClassTag clsTag = clsTagRef;
            if (clsTag != null)
                return clsTag;

            TypeNameTag name = new TypeNameTag("mscorlib", typeNamespace, typeName);
            name = compiler.TagRepository.InternTypeName(name);

            clsTag = new TypeSpecClassTag(name, new TypeSpecTag[0]);
            clsTag = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(clsTag);

            clsTagRef = clsTag;
            return clsTag;
        }

        public PodFlags GetClassPodFlags(Compiler compiler, CliClass cls)
        {
            PodFlags podFlags = PodFlags.None;
            foreach (HighField fld in cls.InstanceFields)
            {
                TypeSpecTag fldType = fld.Type;

                if (!compiler.TypeIsValueType(fldType))
                    return PodFlags.None;

                CliClass fldClass = compiler.GetClosedClass((TypeSpecClassTag)fldType);

                podFlags &= GetClassPodFlags(compiler, fldClass);
                if (podFlags == PodFlags.None)
                    return PodFlags.None;
            }

            return podFlags;
        }

        public MethodHandle GetSystemObjectGetType(Compiler compiler)
        {
            if (m_objectGetTypeHandle != null)
                return m_objectGetTypeHandle;

            TypeSpecClassTag objectType = GetSystemObjectType(compiler);

            MethodSignatureTag signature = new MethodSignatureTag(0, GetSystemTypeType(compiler), new MethodSignatureParam[0]);
            signature = compiler.TagRepository.InternMethodSignature(signature);

            MethodDeclTag declTag = new MethodDeclTag("GetType", signature, objectType.TypeName);
            declTag = compiler.TagRepository.InternMethodDeclTag(declTag);

            MethodSpecTag methodSpec = new MethodSpecTag(MethodSlotType.Instance, new TypeSpecTag[0], GetSystemObjectType(compiler), declTag);
            methodSpec = compiler.TagRepository.InternMethodSpec(methodSpec);

            MethodHandle methodHandle = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), null);
            m_objectGetTypeHandle = methodHandle;

            return methodHandle;
        }

        public uint GetGetHashCodeVTableSlot(Compiler compiler)
        {
            if (m_getHashCodeVTableSlot.HasValue)
                return m_getHashCodeVTableSlot.Value;

            MethodSignatureTag signature = new MethodSignatureTag(0, GetSystemInt32Type(compiler), new MethodSignatureParam[0]);
            signature = compiler.TagRepository.InternMethodSignature(signature);

            MethodDeclTag declTag = new MethodDeclTag("GetHashCode", signature, GetSystemObjectType(compiler).TypeName);
            declTag = compiler.TagRepository.InternMethodDeclTag(declTag);

            CliClass cls = compiler.GetClosedClass(GetSystemObjectType(compiler));
            uint result = cls.DeclTagToVTableSlot[declTag];

            m_getHashCodeVTableSlot = result;

            return result;
        }

        public uint GetEqualsVTableSlot(Compiler compiler)
        {
            if (m_equalsVTableSlot.HasValue)
                return m_equalsVTableSlot.Value;

            MethodSignatureParam[] sigParams = new MethodSignatureParam[1];
            sigParams[0] = new MethodSignatureParam(GetSystemObjectType(compiler), new MethodSignatureParamTypeOfType(MethodSignatureParamTypeOfType.Values.Value));

            MethodSignatureTag signature = new MethodSignatureTag(0, GetSystemBoolType(compiler), sigParams);
            signature = compiler.TagRepository.InternMethodSignature(signature);

            MethodDeclTag declTag = new MethodDeclTag("Equals", signature, GetSystemObjectType(compiler).TypeName);
            declTag = compiler.TagRepository.InternMethodDeclTag(declTag);

            CliClass cls = compiler.GetClosedClass(GetSystemObjectType(compiler));
            uint result = cls.DeclTagToVTableSlot[declTag];

            m_equalsVTableSlot = result;

            return result;
        }

        public MethodDeclTag GetCompareFieldsDeclTag(Compiler compiler)
        {
            if (m_fieldsEqualMethodDecl != null)
                return m_fieldsEqualMethodDecl;

            MethodSignatureParam[] sigParams = new MethodSignatureParam[2];

            TypeSpecGenericParamTag m0Type = new TypeSpecGenericParamTag(new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.MVar), 0);
            m0Type = (TypeSpecGenericParamTag)compiler.TagRepository.InternTypeSpec(m0Type);

            sigParams[0] = new MethodSignatureParam(m0Type, new MethodSignatureParamTypeOfType(MethodSignatureParamTypeOfType.Values.ByRef));
            sigParams[1] = sigParams[0];

            MethodSignatureTag signature = new MethodSignatureTag(1, GetSystemBoolType(compiler), sigParams);
            signature = compiler.TagRepository.InternMethodSignature(signature);

            MethodDeclTag declTag = new MethodDeclTag("FieldsEqual", signature, GetClarityToolsType(compiler).TypeName);
            declTag = compiler.TagRepository.InternMethodDeclTag(declTag);

            m_fieldsEqualMethodDecl = declTag;

            return declTag;
        }

        public MethodDeclTag GetHashBytesDeclTag(Compiler compiler)
        {
            if (m_hashBytesMethodDecl != null)
                return m_hashBytesMethodDecl;

            MethodSignatureParam[] sigParams = new MethodSignatureParam[1];

            TypeSpecGenericParamTag m0Type = new TypeSpecGenericParamTag(new TypeSpecGenericParamTypeTag(TypeSpecGenericParamTypeTag.Values.MVar), 0);
            m0Type = (TypeSpecGenericParamTag)compiler.TagRepository.InternTypeSpec(m0Type);

            sigParams[0] = new MethodSignatureParam(m0Type, new MethodSignatureParamTypeOfType(MethodSignatureParamTypeOfType.Values.ByRef));

            MethodSignatureTag signature = new MethodSignatureTag(1, GetSystemInt32Type(compiler), sigParams);
            signature = compiler.TagRepository.InternMethodSignature(signature);

            MethodDeclTag declTag = new MethodDeclTag("HashBytes", signature, GetClarityToolsType(compiler).TypeName);
            declTag = compiler.TagRepository.InternMethodDeclTag(declTag);

            m_hashBytesMethodDecl = declTag;

            return declTag;
        }
    }
}
