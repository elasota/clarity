using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    // NOTE: This does NOT perform extensive vtable validation, that should be done in CliClass construction
    public class RloVTable
    {
        private MethodInstantiationPath m_instantiationPath;
        private Dictionary<uint, uint> m_instrSlotToRealSlot;
        private MethodHandle[] m_methods;

        public MethodInstantiationPath InstantiationPath { get { return m_instantiationPath; } set { m_instantiationPath = value; } }
        public MethodHandle[] MethodHandles { get { return m_methods; } }

        public void GenerateFromTypeSpec(Compiler compiler, TypeSpecTag typeSpec, VTableGenerationCache vtCache)
        {
            List<MethodHandle> methodHandles;
            if (typeSpec is TypeSpecClassTag)
                methodHandles = GenerateVTableForClass(compiler, (TypeSpecClassTag)typeSpec);
            else if (typeSpec is TypeSpecBoxTag)
                methodHandles = GenerateVTableForBox(compiler, (TypeSpecBoxTag)typeSpec, vtCache);
            else if (typeSpec is TypeSpecDelegateTag)
                methodHandles = GenerateVTableForDelegate(compiler, (TypeSpecDelegateTag)typeSpec, vtCache);
            else if (typeSpec is TypeSpecArrayTag)
                methodHandles = GenerateVTableForArray(compiler, (TypeSpecArrayTag)typeSpec);
            else if (typeSpec is TypeSpecMulticastDelegateTag)
                methodHandles = GenerateVTableForMulticastDelegate(compiler, (TypeSpecMulticastDelegateTag)typeSpec, vtCache);
            else
                throw new ArgumentException("Invalid typespec to generate an array from");

            List<MethodHandle> finalHandles = new List<MethodHandle>();
            m_instrSlotToRealSlot = new Dictionary<uint, uint>();
            uint realSlot = 0;
            uint instrSlot = 0;
            foreach (MethodHandle hdl in methodHandles)
            {
                if (hdl == null)
                {
                    instrSlot++;
                    continue;
                }

                finalHandles.Add(hdl);

                m_instrSlotToRealSlot.Add(instrSlot, realSlot);

                instrSlot++;
                realSlot++;
            }

            m_methods = finalHandles.ToArray();
        }

        private List<MethodHandle> GenerateVTableForArray(Compiler compiler, TypeSpecArrayTag typeSpec)
        {
            TypeSpecClassTag baseClass;

            if (typeSpec.IsSZArray)
            {
                TypeSpecTag subscriptType = typeSpec.SubscriptType;
                if (compiler.TypeIsValueType(typeSpec.SubscriptType))
                {
                    TypeSpecClassTag subscriptClassTag = (TypeSpecClassTag)subscriptType;
                    TypeNameTag subscriptClassName = subscriptClassTag.TypeName;

                    if (subscriptClassName.FastIs("mscorlib", "System", "Nullable`1", 1, null))
                    {
                        TypeNameTag baseName = new TypeNameTag("mscorlib", "Clarity", "NullableSZArray`1", 1, null);
                        baseName = compiler.TagRepository.InternTypeName(baseName);

                        baseClass = new TypeSpecClassTag(baseName, subscriptClassTag.ArgTypes);
                        baseClass = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(baseClass);
                    }
                    else
                    {
                        TypeNameTag baseName = new TypeNameTag("mscorlib", "Clarity", "ValueSZArray`1", 1, null);
                        baseName = compiler.TagRepository.InternTypeName(baseName);

                        baseClass = new TypeSpecClassTag(baseName, new TypeSpecTag[] { subscriptClassTag });
                        baseClass = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(baseClass);
                    }
                }
                else
                {
                    TypeNameTag baseName = new TypeNameTag("mscorlib", "Clarity", "RefSZArray");
                    baseName = compiler.TagRepository.InternTypeName(baseName);

                    baseClass = new TypeSpecClassTag(baseName, new TypeSpecTag[0]);
                    baseClass = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(baseClass);
                }
            }
            else
                throw new NotImplementedException("Multidimensional arrays not implemented");

            return GenerateVTableForClass(compiler, baseClass);
        }

        private List<MethodHandle> GenerateVTableForMulticastDelegate(Compiler compiler, TypeSpecMulticastDelegateTag typeSpec, VTableGenerationCache vtCache)
        {
            TypeSpecClassTag delegateType = typeSpec.DelegateType;
            CliClass cls = compiler.GetClosedClass(delegateType);

            List<MethodHandle> methodHandles = new List<MethodHandle>();

            List<MethodSpecTag> methodSpecs = new List<MethodSpecTag>();
            foreach (CliVtableSlot vtableSlot in cls.VTable)
            {
                CliMethodIndex methodIndex = vtableSlot.MethodIndex;
                if (methodIndex == null)
                    methodSpecs.Add(null);
                else
                    methodSpecs.Add(ResolveVirtualMethod(compiler, cls, methodIndex));
            }

            int numSlots = methodSpecs.Count;
            for (int i = 0; i < numSlots; i++)
                methodHandles.Add(null);

            foreach (KeyValuePair<MethodDeclTag, uint> dtvs in cls.DeclTagToVTableSlot)
            {
                int slot = (int)dtvs.Value;
                MethodDeclTag methodDecl = dtvs.Key;

                if (methodDecl.Name == "Invoke")
                {
                    methodHandles[slot] = compiler.InstantiateMethod(new GeneratedMethods.GMMulticastDelegateInvoke(typeSpec.DelegateType, vtCache), m_instantiationPath);
                    methodSpecs[slot] = null;
                }
            }

            for (int i = 0; i < numSlots; i++)
            {
                MethodSpecTag methodSpec = methodSpecs[i];
                if (methodSpec != null)
                    methodHandles[i] = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), m_instantiationPath);
            }

            return methodHandles;
        }

        private List<MethodHandle> GenerateVTableForDelegate(Compiler compiler, TypeSpecDelegateTag typeSpec, VTableGenerationCache vtCache)
        {
            TypeSpecClassTag delegateType = typeSpec.DelegateType;
            CliClass cls = compiler.GetClosedClass(delegateType);

            List<MethodHandle> methodHandles = new List<MethodHandle>();

            List<MethodSpecTag> methodSpecs = new List<MethodSpecTag>();
            foreach (CliVtableSlot vtableSlot in cls.VTable)
            {
                CliMethodIndex methodIndex = vtableSlot.MethodIndex;
                if (methodIndex == null)
                    methodSpecs.Add(null);
                else
                    methodSpecs.Add(ResolveVirtualMethod(compiler, cls, methodIndex));
            }

            int numSlots = methodSpecs.Count;
            for (int i = 0; i < numSlots; i++)
                methodHandles.Add(null);

            foreach (KeyValuePair<MethodDeclTag, uint> dtvs in cls.DeclTagToVTableSlot)
            {
                int slot = (int)dtvs.Value;
                MethodDeclTag methodDecl = dtvs.Key;

                if (methodDecl.Name == "ConvertToMulticastImpl")
                {
                    methodHandles[slot] = compiler.InstantiateMethod(new GeneratedMethods.GMDelegateConvertToMulticast(typeSpec, vtCache), m_instantiationPath);
                    methodSpecs[slot] = null;
                }
                else if (methodDecl.Name == "Invoke")
                {
                    methodHandles[slot] = compiler.InstantiateMethod(new GeneratedMethods.GMDelegateInvoke(typeSpec), m_instantiationPath);
                    methodSpecs[slot] = null;
                }
            }

            for (int i = 0; i < numSlots; i++)
            {
                MethodSpecTag methodSpec = methodSpecs[i];
                if (methodSpec != null)
                    methodHandles[i] = compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), m_instantiationPath);
            }

            return methodHandles;
        }

        private List<MethodHandle> GenerateVTableForBox(Compiler compiler, TypeSpecBoxTag typeSpec, VTableGenerationCache vtCache)
        {
            TypeSpecClassTag containedType = typeSpec.ContainedType;
            List<MethodSpecTag> baseSpecs = GenerateMethodSpecsForClass(compiler, containedType);

            IList<MethodSpecTag> vtMethodSpecs = vtCache.ValueTypeMethodSpecs;

            if (vtMethodSpecs == null)
            {
                TypeNameTag valueTypeName = new TypeNameTag("mscorlib", "System", "ValueType");
                valueTypeName = compiler.TagRepository.InternTypeName(valueTypeName);

                TypeSpecClassTag valueTypeSpec = new TypeSpecClassTag(valueTypeName, new TypeSpecTag[0]);
                valueTypeSpec = (TypeSpecClassTag)compiler.TagRepository.InternTypeSpec(valueTypeSpec);

                vtMethodSpecs = GenerateMethodSpecsForClass(compiler, valueTypeSpec);
                vtCache.ValueTypeMethodSpecs = vtMethodSpecs;
            }

            List<MethodHandle> methodHandles = new List<MethodHandle>();

            for (int slotIndex = 0; slotIndex < baseSpecs.Count; slotIndex++)
            {
                MethodSpecTag methodSpec = baseSpecs[slotIndex];
                if (methodSpec == null)
                {
                    methodHandles.Add(null);
                    continue;
                }

                if (slotIndex < vtMethodSpecs.Count && vtMethodSpecs[slotIndex] == methodSpec)
                {
                    MethodSpecTag slotSpec = vtMethodSpecs[slotIndex];
                    if (slotSpec == methodSpec)
                    {
                        MethodDeclTag slotDecl = vtMethodSpecs[slotIndex].MethodDecl;

                        // Generate implementations for value-sensitive slots
                        if (slotDecl.Name == "Equals")
                        {
                            methodHandles.Add(compiler.InstantiateMethod(new GeneratedMethods.GMBoxedValueTypeEquals(typeSpec, vtCache), m_instantiationPath));
                            continue;
                        }
                        else if (slotDecl.Name == "GetHashCode")
                        {
                            methodHandles.Add(compiler.InstantiateMethod(new GeneratedMethods.GMBoxedValueTypeGetHashCode(typeSpec, vtCache), m_instantiationPath));
                            continue;
                        }
                    }
                }

                // If not sensitive, then use the method spec and generate a box thunk
                methodHandles.Add(compiler.InstantiateMethod(new GeneratedMethods.GMBoxThunk(methodSpec), m_instantiationPath));
            }

            return methodHandles;
        }

        private List<MethodHandle> GenerateVTableForClass(Compiler compiler, TypeSpecClassTag typeSpec)
        {
            List<MethodSpecTag> methodSpecs = GenerateMethodSpecsForClass(compiler, typeSpec);

            List<MethodHandle> methodHandles = new List<MethodHandle>();
            foreach (MethodSpecTag methodSpec in methodSpecs)
            {
                if (methodSpec == null)
                    methodHandles.Add(null);
                else
                    methodHandles.Add(compiler.InstantiateMethod(new MethodSpecMethodKey(methodSpec), m_instantiationPath));
            }

            return methodHandles;
        }

        private MethodSpecTag ResolveVirtualMethod(Compiler compiler, CliClass cls, CliMethodIndex methodIndex)
        {
            uint depth = methodIndex.Depth;
            CliClass methodClass = cls;
            while (depth > 0)
            {
                methodClass = methodClass.ParentClass;
                depth--;
            }

            HighMethod method = methodClass.Methods[methodIndex.Index];
            if (method.IsStatic)
                throw new RpaCompileException("Vtable slot implementation is static");

            MethodSpecTag methodSpec = new MethodSpecTag(MethodSlotType.Instance, new TypeSpecTag[0], methodClass.TypeSpec, method.MethodDeclTag);
            return compiler.TagRepository.InternMethodSpec(methodSpec);
        }

        private List<MethodSpecTag> GenerateMethodSpecsForClass(Compiler compiler, TypeSpecClassTag typeSpec)
        {
            CliClass cls = compiler.GetClosedClass(typeSpec);

            List<MethodSpecTag> methodSpecs = new List<MethodSpecTag>();
            foreach (CliVtableSlot vtableSlot in cls.VTable)
            {
                if (vtableSlot.MethodSignature.NumGenericParameters > 0)
                {
                    methodSpecs.Add(null);
                    continue;
                }

                CliMethodIndex methodIndex = vtableSlot.MethodIndex;
                if (methodIndex == null)
                    throw new RpaCompileException("Can't generate vtable slot for abstract method");

                methodSpecs.Add(ResolveVirtualMethod(compiler, cls, methodIndex));
            }

            return methodSpecs;
        }
    }
}
