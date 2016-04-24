using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CppMethod
    {
        public CLRMethodSignatureInstance DeclaredMethodSignature { get; private set; }
        public CLRMethodSignatureInstance MethodSignature { get; private set; }

        public CLRTypeDefRow DeclaredInClass { get; private set; }
        public CLRTypeSpec DeclaredInClassSpec { get; private set; }
        public Clarity.Rpa.MethodDeclTag VtableSlotTag { get; private set; }
        public string Name { get; private set; }
        public bool Virtual { get; private set; }
        public bool Static { get; private set; }
        public bool Abstract { get; private set; }
        public bool Overrides { get; private set; }
        public bool Final { get; private set; }
        public CppVtableSlot ReplacesStandardSlot { get; set; }
        public CppVtableSlot CreatesSlot { get; private set; }
        public uint NumGenericParameters { get; private set; }
        public CLRMethodDefRow MethodDef { get; private set; }

        public CppMethod(CLRAssemblyCollection assemblies, CLRTypeDefRow declaredInClass, CLRMethodDefRow methodDef)
        {
            // Hack: Solve whether value type?
            DeclaredInClassSpec = CppBuilder.CreateInstanceTypeSpec(assemblies, declaredInClass);
            DeclaredInClass = declaredInClass;
            MethodSignature = new CLRMethodSignatureInstance(assemblies, methodDef.Signature);
            DeclaredMethodSignature = MethodSignature;
            Name = methodDef.Name;
            Virtual = methodDef.Virtual;
            Abstract = methodDef.Abstract;
            Static = methodDef.Static;
            Final = methodDef.Final;
            Overrides = (methodDef.VtableLayout == CLRMethodDefRow.MethodVtableLayout.ReuseSlot);
            MethodDef = methodDef;

            if (methodDef.Signature.NumGenericParameters > 0 && this.Virtual && !this.Final && declaredInClass.Semantics != CLRTypeDefRow.TypeSemantics.Interface)
                throw new NotSupportedException(DeclaredInClassSpec.ToString() + "." + this.Name + " is a non-final virtual generic method, which is only supported on interfaces.");

            Clarity.Rpa.MethodSignatureTag methodSigTag = CppExport.RpaTagFactory.CreateMethodSignature(this.MethodSignature);

            uint numGenericParameters = 0;
            if (methodDef.GenericParameters != null)
                numGenericParameters = (uint)methodDef.GenericParameters.Length;

            Clarity.Rpa.TypeNameTag declaredInClassTag = RpaTagFactory.CreateTypeNameTag(declaredInClass);
            VtableSlotTag = new Clarity.Rpa.MethodDeclTag(methodDef.Name, methodSigTag, declaredInClassTag);
            if (methodDef.Virtual && !Overrides)
            {
                bool isGenericInterface = ((methodDef.Owner.Semantics == CLRTypeDefRow.TypeSemantics.Interface) && methodDef.Owner.GenericParameters != null && methodDef.Owner.GenericParameters.Length > 0);

                CreatesSlot = new CppVtableSlot(MethodSignature, DeclaredInClassSpec, Name, VtableSlotTag, isGenericInterface, methodDef);
            }

            NumGenericParameters = numGenericParameters;
        }

        private CppMethod(CppMethod baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            DeclaredMethodSignature = baseInstance.DeclaredMethodSignature;
            MethodSignature = baseInstance.MethodSignature.Instantiate(typeParams, methodParams);
            DeclaredInClass = baseInstance.DeclaredInClass;
            DeclaredInClassSpec = baseInstance.DeclaredInClassSpec.Instantiate(typeParams, methodParams);
            VtableSlotTag = baseInstance.VtableSlotTag;
            Name = baseInstance.Name;
            Virtual = baseInstance.Virtual;
            Abstract = baseInstance.Abstract;
            Static = baseInstance.Static;
            if (baseInstance.CreatesSlot != null)
                CreatesSlot = baseInstance.CreatesSlot.Instantiate(typeParams, methodParams);
            Final = baseInstance.Final;
            NumGenericParameters = baseInstance.NumGenericParameters;
            MethodDef = baseInstance.MethodDef;
        }

        public CppMethod Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CppMethod(this, typeParams, methodParams);
        }

        public string GenerateBaseName()
        {
            return "!TODO FIX ME!";
            /*
            string methodBaseName = MethodDef.Static ? "s" : "i";
            methodBaseName += CppBuilder.LegalizeName(Name, true);
            if (GenericTypeParamMangle != null)
                methodBaseName += GenericTypeParamMangle;
            if (GenericMethodParamMangle != null)
                methodBaseName += GenericMethodParamMangle;
            return methodBaseName;*/
        }

        public string GenerateCallName()
        {
            return "mcall_" + GenerateBaseName();
        }

        public string GenerateCodeName()
        {
            return "mcode_" + GenerateBaseName();
        }

        public void ResolveOverrides(List<CppVtableSlot> overridableSlots)
        {
            if (!this.Virtual)
                throw new ArgumentException();

            HashSet<CppVtableSlot> overrided = new HashSet<CppVtableSlot>();
            foreach (CppVtableSlot slot in overridableSlots)
            {
                if (slot.Name == this.Name && slot.Signature.Equals(this.MethodSignature))
                    overrided.Add(slot);
            }

            int n = 0;
            foreach (CppVtableSlot slot in overrided)
            {
                n++;
                overridableSlots.Remove(slot);

                if (this.Overrides)
                {
                    if (n != 1)
                        throw new ParseFailedException("Method overrides multiple virtual methods");
                    ReplacesStandardSlot = slot;
                }
            }
        }
    }
}
