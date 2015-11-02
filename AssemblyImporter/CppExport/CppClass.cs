using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppClass
    {
        public string Name { get; private set; }
        public string FullName { get; private set; }
        public bool IsValueType { get; private set; }
        public bool IsEnum { get; private set; }
        public bool IsMulticastDelegate { get; private set; }
        public bool IsDelegate { get; private set; }

        public IEnumerable<CppMethod> Methods { get { return m_methods; } }
        public IEnumerable<CppField> Fields { get { return m_fields; } }
        public IEnumerable<CppField> InheritedFields { get { return m_inheritedFields; } }
        public CLRTypeSpec ParentTypeSpec { get; private set; }
        public int NumGenericParameters { get; private set; }
        public bool HaveAnyNewlyImplementedInterfaces { get { return m_newlyImplementedInterfaces.Count > 0; } }
        public IEnumerable<CLRTypeSpec> NewlyImplementedInterfaces { get { return m_newlyImplementedInterfaces; } }
        public IEnumerable<CLRTypeSpec> ReimplementedInterfaces { get { return m_reimplementedInterfaces; } }
        public IEnumerable<CLRTypeSpec> InheritedImplementedInterfaces { get { return m_inheritedImplementedInterfaces; } }
        public IEnumerable<CLRTypeSpec> PassiveInterfaceConversions { get { return m_passiveIfcConversions; } }

        public IEnumerable<CppVtableSlot> AllVtableSlots { get { return m_allVtableSlots; } }
        public IEnumerable<CppVtableSlot> VisibleVtableSlots { get { return m_visibleVtableSlots; } }

        public CLRTypeDefRow TypeDef { get { return m_typeDef; } }
        public IEnumerable<CLRTypeSpec> GenericParameters { get { return m_genericParameters; } }

        public CLRMethodSignatureInstance DelegateSignature { get; private set; }
        public bool HaveStaticFields { get; private set; }

        private List<CppMethod> m_methods;
        private List<CppVtableSlot> m_allVtableSlots;       // All slots
        private List<CppVtableSlot> m_visibleVtableSlots;   // Slots that haven't been overlapped by a NewSlot
        private List<CppField> m_fields;
        private List<CppField> m_inheritedFields;
        private List<CLRTypeSpec> m_newlyImplementedInterfaces;
        private List<CLRTypeSpec> m_reimplementedInterfaces;
        private List<CLRTypeSpec> m_inheritedImplementedInterfaces;
        private List<CLRTypeSpec> m_passiveIfcConversions;
        private List<CLRTypeSpec> m_inheritedPassiveIfcConversions;
        private List<CLRTypeSpec> m_genericParameters;

        private CLRTypeDefRow m_typeDef;

        public CppClass(CLRTypeSpecClass ts)
        {
            m_methods = new List<CppMethod>();
            m_fields = new List<CppField>();
            m_allVtableSlots = new List<CppVtableSlot>();
            m_visibleVtableSlots = new List<CppVtableSlot>();
            m_inheritedFields = new List<CppField>();
            m_reimplementedInterfaces = new List<CLRTypeSpec>();
            m_newlyImplementedInterfaces = new List<CLRTypeSpec>();
            m_inheritedImplementedInterfaces = new List<CLRTypeSpec>();
            m_passiveIfcConversions = new List<CLRTypeSpec>();
            m_inheritedPassiveIfcConversions = new List<CLRTypeSpec>();
            m_genericParameters = new List<CLRTypeSpec>();

            if (ts.TypeDef.GenericParameters != null)
            {
                NumGenericParameters = ts.TypeDef.GenericParameters.Length;
                for (int i = 0; i < NumGenericParameters; i++)
                    m_genericParameters.Add(new CLRTypeSpecVarOrMVar(CLRSigType.ElementType.VAR, (uint)i));
            }

            m_typeDef = ts.TypeDef;

            Name = ts.TypeDef.TypeName;
            FullName = GenerateFullPath(ts.TypeDef);
        }

        private CppClass(CppClass baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            if (typeParams.Length != baseInstance.NumGenericParameters)
                throw new ArgumentException();

            m_methods = new List<CppMethod>();
            m_fields = new List<CppField>();
            m_allVtableSlots = new List<CppVtableSlot>();
            m_visibleVtableSlots = new List<CppVtableSlot>();
            m_inheritedFields = new List<CppField>();
            m_reimplementedInterfaces = new List<CLRTypeSpec>();
            m_newlyImplementedInterfaces = new List<CLRTypeSpec>();
            m_inheritedImplementedInterfaces = new List<CLRTypeSpec>();
            m_passiveIfcConversions = new List<CLRTypeSpec>();
            m_inheritedPassiveIfcConversions = new List<CLRTypeSpec>();
            m_genericParameters = new List<CLRTypeSpec>();

            m_typeDef = baseInstance.m_typeDef;

            Name = baseInstance.Name;
            FullName = baseInstance.FullName;
            if (baseInstance.ParentTypeSpec != null)
                ParentTypeSpec = baseInstance.ParentTypeSpec.Instantiate(typeParams, methodParams);

            NumGenericParameters = baseInstance.NumGenericParameters;
            m_genericParameters.AddRange(typeParams);

            foreach (CppMethod method in baseInstance.m_methods)
                m_methods.Add(method.Instantiate(typeParams, methodParams));
            foreach (CppField field in baseInstance.m_fields)
                m_fields.Add(field.Instantiate(typeParams, methodParams));

            foreach (CppVtableSlot vts in baseInstance.m_visibleVtableSlots)
                m_visibleVtableSlots.Add(vts.Instantiate(typeParams, methodParams));
            foreach (CppVtableSlot vts in baseInstance.m_allVtableSlots)
                m_allVtableSlots.Add(vts.Instantiate(typeParams, methodParams));

            foreach (CppField field in baseInstance.m_inheritedFields)
                m_inheritedFields.Add(field.Instantiate(typeParams, methodParams));
            foreach (CLRTypeSpec impl in baseInstance.m_newlyImplementedInterfaces)
                m_newlyImplementedInterfaces.Add(impl.Instantiate(typeParams, methodParams));
            foreach (CLRTypeSpec impl in baseInstance.m_reimplementedInterfaces)
                m_reimplementedInterfaces.Add(impl.Instantiate(typeParams, methodParams));
            foreach (CLRTypeSpec impl in baseInstance.m_inheritedImplementedInterfaces)
                m_inheritedImplementedInterfaces.Add(impl.Instantiate(typeParams, methodParams));
            foreach (CLRTypeSpec impl in baseInstance.m_passiveIfcConversions)
                m_passiveIfcConversions.Add(impl.Instantiate(typeParams, methodParams));
            foreach (CLRTypeSpec impl in baseInstance.m_inheritedPassiveIfcConversions)
                m_inheritedPassiveIfcConversions.Add(impl.Instantiate(typeParams, methodParams));

            IsDelegate = baseInstance.IsDelegate;
            IsMulticastDelegate = baseInstance.IsMulticastDelegate;
            IsEnum = baseInstance.IsEnum;
            IsValueType = baseInstance.IsValueType;
            if (DelegateSignature != null)
                DelegateSignature = baseInstance.DelegateSignature.Instantiate(typeParams, methodParams);
            HaveStaticFields = baseInstance.HaveStaticFields;
        }

        public CppClass Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CppClass(this, typeParams, methodParams);
        }

        public void AddField(CLRAssemblyCollection assemblies, CLRFieldRow field)
        {
            if (field.Static && !field.Literal)
                HaveStaticFields = true;
            m_fields.Add(new CppField(assemblies, field));
        }

        public void AddMethod(CLRAssemblyCollection assemblies, CLRMethodDefRow method)
        {
            CppMethod cppMethod = new CppMethod(assemblies, m_typeDef, method);
            m_methods.Add(cppMethod);
            if (cppMethod.Virtual && !cppMethod.Overrides)
            {
                m_visibleVtableSlots.Add(cppMethod.CreatesSlot);
                m_allVtableSlots.Add(cppMethod.CreatesSlot);
            }
        }

        private void AddPassiveConversion(CLRTypeSpec ifcTypeSpec)
        {
            bool isMatch = false;
            foreach (CLRTypeSpec ifc in m_passiveIfcConversions)
            {
                if (ifc.Equals(ifcTypeSpec))
                {
                    isMatch = true;
                    break;
                }
            }
            if (!isMatch)
            {
                foreach (CLRTypeSpec ifc in m_inheritedPassiveIfcConversions)
                {
                    if (ifc.Equals(ifcTypeSpec))
                    {
                        isMatch = true;
                        break;
                    }
                }
            }

            if (!isMatch)
                m_passiveIfcConversions.Add(ifcTypeSpec);
        }

        public void AddExplicitInterface(CppBuilder builder, CLRTypeSpec ifcTypeSpec)
        {
            CppClass ifcType = builder.GetCachedClass(ifcTypeSpec);

            // CS0695 guarantees that type substitution will never result in multiple interfaces
            // resolving to the same passive conversion, so this strategy should be OK
            foreach (CLRTypeSpec ifc in ifcType.m_newlyImplementedInterfaces)
            {
                AddExplicitInterface(builder, ifc);
                //AddPassiveConversion(ifc);
            }

            // Only add each explicit interface once
            foreach (CLRTypeSpec ifc in m_newlyImplementedInterfaces)
                if (ifc.Equals(ifcTypeSpec))
                    return;

            // Unique, add it
            m_newlyImplementedInterfaces.Add(ifcTypeSpec);
        }

        public static void RemoveOverrides(List<CppVtableSlot> visible, CppVtableSlot slot)
        {
            List<int> removeIndexes = FindOverrideIndexes(visible, slot.InternalName, slot.Signature);
            for (int i = 0; i < removeIndexes.Count; i++)
                visible.RemoveAt(removeIndexes[removeIndexes.Count - 1 - i]);
        }

        public static List<int> FindOverrideIndexes(List<CppVtableSlot> visible, string name, CLRMethodSignatureInstance sig)
        {
            List<int> removeIndexes = new List<int>();
            for (int i = 0; i < visible.Count; i++)
            {
                CppVtableSlot candidate = visible[i];

                if (candidate.InternalName == name && candidate.Signature.Equals(sig))
                    removeIndexes.Add(i);
            }
            return removeIndexes;
        }

        public void ResolveInherit(CppBuilder builder, CppClass parentClass, IEnumerable<CLRTypeSpec> interfaces, CLRTypeSpec parentTypeSpec)
        {
            // There are a LOT of tricky slotting cases here.
            // See TestNewSlotImplementation, TestSlotDivergence, and TestImplementNonVirtual for some sample cases.
            //
            // Roughly how this works:
            // 1.) Override vtable slots based on matching:
            //     - If a method is ReuseSlot, then it overrides a slot no matter what
            //     - If a method is NewSlot, then it creates a slot
            // 2.) Implement MethodImpls and interfaces as cross-slot thunks
            //
            // Two notable complications with this:
            //
            // In Clarity, we only implement the specified interface once in a given class's heirarchy, but
            // reimplemented interfaces need to emit new mappings because the reimplementation can change how the
            // interface is implemented.  For example, if a parent class implements an interface method by match,
            // and then a derived class hides that method with a newslot and reimplements the interface, then the
            // interface must link to the newslot method.
            // 
            // We might be able to optimize this a bit if we can detect that a method reimplementation is the
            // same as the one that already exists.
            //
            // The other complication is that Roslyn sometimes emits useless but apparently legal .override
            // directives that "override" a parent class implementation with the same method that already overrides
            // it from reuseslot matching.

            ParentTypeSpec = parentTypeSpec;

            if (parentClass != null)
            {
                m_allVtableSlots.AddRange(parentClass.m_allVtableSlots);

                List<CppVtableSlot> parentVisibleSlots = new List<CppVtableSlot>();
                parentVisibleSlots.AddRange(parentClass.m_visibleVtableSlots);
                foreach (CppMethod method in m_methods)
                {
                    if (method.Virtual)
                    {
                        if (method.CreatesSlot != null)
                            RemoveOverrides(parentVisibleSlots, method.CreatesSlot);

                        if (method.Overrides)
                        {
                            List<int> overrideIndexes = FindOverrideIndexes(parentVisibleSlots, method.Name, method.MethodSignature);
                            if (overrideIndexes.Count != 1)
                                throw new ParseFailedException("Method did not override exactly one slot");
                            method.ReplacesStandardSlot = parentVisibleSlots[overrideIndexes[0]];
                        }
                    }
                }

                m_visibleVtableSlots.AddRange(parentVisibleSlots);
            }

            if (parentClass != null)
            {
                // Remove already-implemented interfaces from this class's set, but keep reimplementations
                // to resolve them again
                List<CLRTypeSpec> dedupedInterfaces = new List<CLRTypeSpec>();
                List<CLRTypeSpec> reimplementedInterfaces = new List<CLRTypeSpec>();
                List<CLRTypeSpec> allInheritedInterfaces = new List<CLRTypeSpec>();

                allInheritedInterfaces.AddRange(parentClass.m_newlyImplementedInterfaces);
                allInheritedInterfaces.AddRange(parentClass.m_inheritedImplementedInterfaces);
                foreach (CLRTypeSpec ifc in m_newlyImplementedInterfaces)
                {
                    if (allInheritedInterfaces.Contains(ifc))
                        reimplementedInterfaces.Add(ifc);
                    else
                        dedupedInterfaces.Add(ifc);
                }

                m_newlyImplementedInterfaces = dedupedInterfaces;
                m_reimplementedInterfaces = reimplementedInterfaces;

                m_inheritedFields.AddRange(parentClass.m_inheritedFields);
                m_inheritedFields.AddRange(parentClass.m_fields);

                m_inheritedImplementedInterfaces = allInheritedInterfaces;

                m_inheritedPassiveIfcConversions.AddRange(parentClass.m_passiveIfcConversions);
                m_inheritedPassiveIfcConversions.AddRange(parentClass.m_inheritedPassiveIfcConversions);

                IsValueType = (parentClass.FullName == "System.ValueType" && this.FullName != "System.Enum") || parentClass.FullName == "System.Enum";
                IsEnum = parentClass.FullName == "System.Enum";

                IsMulticastDelegate = (parentClass.FullName == "System.MulticastDelegate");
                IsDelegate = IsMulticastDelegate || (parentClass.FullName == "System.Delegate" && this.FullName != "System.MulticastDelegate");
                HaveStaticFields = (HaveStaticFields || parentClass.HaveStaticFields);

                if (IsDelegate)
                {
                    CLRMethodSignatureInstance delegateSig = null;
                    foreach (CppMethod method in m_methods)
                    {
                        if (method.Name == "Invoke")
                        {
                            DelegateSignature = method.MethodSignature;
                            break;
                        }
                    }

                    if (DelegateSignature == null)
                        throw new ParseFailedException("Malformed delegate");
                }
            }
        }

        public static string GeneratePathForFullName(string fullName, string suffix)
        {
            string[] fullClassPath = fullName.Split('.');

            string path = "";
            for (int i = 0; i < fullClassPath.Length; i++)
            {
                if (i != 0)
                    path += '\\';
                path += CppBuilder.LegalizeName(fullClassPath[i], true);
            }
            path += suffix;
            return path;
        }

        public static string GenerateInstanceCodePathForFullName(string fullName)
        {
            return GeneratePathForFullName(fullName, ".Code.cpp");
        }

        public string GenerateInstanceCodePath()
        {
            return GenerateInstanceCodePathForFullName(FullName);
        }

        public static string GenerateBoxPathForFullName(string fullName)
        {
            return GeneratePathForFullName(fullName, ".Box.h");
        }

        public string GenerateBoxPath()
        {
            return GenerateBoxPathForFullName(FullName);
        }

        public static string GenerateDefinitionPathForFullName(string fullName)
        {
            return GeneratePathForFullName(fullName, ".Def.h");
        }

        public string GenerateDefinitionPath()
        {
            return GenerateDefinitionPathForFullName(FullName);
        }

        public static string GenerateMainHeaderForFullName(string fullName)
        {
            return GeneratePathForFullName(fullName, ".h");
        }

        public string GenerateMainHeaderPath()
        {
            return GenerateMainHeaderForFullName(FullName);
        }

        public static string GeneratePrototypePathForFullName(string fullName)
        {
            return GeneratePathForFullName(fullName, ".Proto.h");
        }

        public string GeneratePrototypePath()
        {
            return GeneratePrototypePathForFullName(FullName);
        }

        public string GenerateInlineCodePath()
        {
            return GeneratePathForFullName(FullName, ".InlineCode.h");
        }

        public string GenerateExportedCodePath()
        {
            return GeneratePathForFullName(FullName, ".ExportedCode.cpp");
        }

        public static string GenerateCppClassNameFromFullName(string fullName)
        {
            string[] fullClassPath = fullName.Split('.');

            string cn = "::CLRX";
            for (int i = 0; i < fullClassPath.Length - 1; i++)
                cn += "::N" + CppBuilder.LegalizeName(fullClassPath[i], true);
            cn += "::" + CppBuilder.LegalizeName(fullClassPath[fullClassPath.Length - 1], true);
            return cn;
        }

        public string GenerateCppClassName()
        {
            return GenerateCppClassNameFromFullName(this.FullName);
        }

        public static string GenerateFullPath(CLRTypeDefRow typeDef)
        {
            string fullName = "";
            if (typeDef.TypeNamespace != "")
                fullName += typeDef.TypeNamespace + ".";
            fullName += typeDef.TypeName;
            if (typeDef.ContainerClass != null)
                fullName = GenerateFullPath(typeDef.ContainerClass) + "." + fullName;
            return fullName;
        }
    }
}
