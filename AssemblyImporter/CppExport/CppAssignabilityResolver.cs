using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

// This has a lot of complicated cases, especially with generic constraints in the mix
// For example, array covariance applies to generic types but only if the generic type
// can be proven to be a reference type.
//
// The rules here are ROUGHLY defined by III.1.8.1.2.3
namespace AssemblyImporter.CppExport
{
    public class CppAssignabilityResolver
    {
        private CppBuilder m_builder;
        private CLRTypeDefRow m_inClass;
        private CLRMethodDefRow m_inMethod;
        private CfgNodeCompiler.CommonTypeLookup m_commonTypes;

        public CppAssignabilityResolver(CppBuilder builder, CfgNodeCompiler.CommonTypeLookup commonTypes, CLRTypeDefRow inClass, CLRMethodDefRow inMethod)
        {
            m_builder = builder;
            m_commonTypes = commonTypes;
            m_inClass = inClass;
            m_inMethod = inMethod;
        }

        public bool IsRefAssignable(CLRTypeSpec from, CLRTypeSpec to)
        {
            if (from.Equals(to))
                return true;

            if (from is CLRTypeSpecClass || from is CLRTypeSpecGenericInstantiation)
            {
                CppClass fromClass = m_builder.GetCachedClass(from);
                return IsClassBasedOn(fromClass, to);
            }
            else if (from is CLRTypeSpecComplexArray)
            {
                if (to.Equals(m_commonTypes.Array) || to.Equals(m_commonTypes.Object))
                    return true;

                if (!(to is CLRTypeSpecComplexArray))
                    return false;

                CLRTypeSpecComplexArray cplxFrom = (CLRTypeSpecComplexArray)from;
                CLRTypeSpecComplexArray cplxTo = (CLRTypeSpecComplexArray)to;

                if (cplxFrom.Rank != cplxTo.Rank)
                    return false;

                for (uint i = 0; i < cplxFrom.Rank; i++)
                    if (cplxFrom.LowBounds[i] != cplxTo.LowBounds[i])
                        return false;

                CLRTypeSpec subscriptFrom = cplxFrom.SubType;
                CLRTypeSpec subscriptTo = cplxTo.SubType;

                if (IsProvablyReferenceType(subscriptFrom))
                    return IsRefAssignable(subscriptFrom, subscriptTo);
                return subscriptFrom.Equals(subscriptTo);
            }
            else if (from is CLRTypeSpecSZArray)
            {
                if (to.Equals(m_commonTypes.Array) || to.Equals(m_commonTypes.Object))
                    return true;

                if (!(to is CLRTypeSpecSZArray))
                    return false;

                CLRTypeSpecSZArray arrayFrom = (CLRTypeSpecSZArray)from;
                CLRTypeSpecSZArray arrayTo = (CLRTypeSpecSZArray)to;

                CLRTypeSpec subscriptFrom = arrayFrom.SubType;
                CLRTypeSpec subscriptTo = arrayTo.SubType;

                if (IsProvablyReferenceType(subscriptFrom))
                    return IsRefAssignable(subscriptFrom, subscriptTo);
                return subscriptFrom.Equals(subscriptTo);
            }
            else if (from is CLRTypeSpecVarOrMVar)
            {
                CLRGenericParamRow genericParam = GenericParamForSpec((CLRTypeSpecVarOrMVar)from);

                if (genericParam.NotNullableValueTypeConstraint)
                    return (to.Equals(m_commonTypes.Object) || to.Equals(m_commonTypes.ValueType));

                if (genericParam.ReferenceTypeConstraint && to.Equals(m_commonTypes.Object))
                    return true;

                foreach (CLRGenericParamConstraintRow constraint in genericParam.Constraints)
                {
                    CLRTypeSpec constraintType = m_builder.Assemblies.InternTypeDefOrRefOrSpec(constraint.Constraint);
                    if (IsRefAssignable(constraintType, to))
                        return true;
                }

                return false;
            }

            throw new ArgumentException();
        }

        private bool IsClassBasedOn(CppClass cls, CLRTypeSpec to)
        {
            foreach (CLRTypeSpec newIfc in cls.NewlyImplementedInterfaces)
            {
                if (newIfc.Equals(to))
                    return true;
            }

            if (cls.ParentTypeSpec == null)
                return false;

            if (cls.ParentTypeSpec.Equals(to))
                return true;

            return IsClassBasedOn(m_builder.GetCachedClass(cls.ParentTypeSpec), to);
        }

        private CLRGenericParamRow GenericParamForSpec(CLRTypeSpecVarOrMVar varOrMVar)
        {
            if (varOrMVar.ElementType == CLRSigType.ElementType.VAR)
                return m_inClass.GenericParameters[varOrMVar.Value];
            else if (varOrMVar.ElementType == CLRSigType.ElementType.MVAR)
                return m_inMethod.GenericParameters[varOrMVar.Value];
            else
                throw new ArgumentException();
        }

        private bool IsProvablyReferenceType(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass || typeSpec is CLRTypeSpecGenericInstantiation)
                return !m_builder.GetCachedClass(typeSpec).IsValueType;
            else if (typeSpec is CLRTypeSpecComplexArray || typeSpec is CLRTypeSpecSZArray)
                return true;
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                CLRGenericParamRow genericParam = GenericParamForSpec((CLRTypeSpecVarOrMVar)typeSpec);

                if (genericParam.ReferenceTypeConstraint)
                    return true;

                if (genericParam.NotNullableValueTypeConstraint)
                    return false;

                // Look for a non-interface class constraint
                // If one exists, then this is a reference type
                foreach (CLRGenericParamConstraintRow constraint in genericParam.Constraints)
                {
                    CLRTypeSpec constraintType = m_builder.Assemblies.InternTypeDefOrRefOrSpec(constraint.Constraint);
                    CppClass constraintClass = m_builder.GetCachedClass(constraintType);
                    if (constraintClass.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                        continue;
                    return true;
                }

                // Found nothing, this could be a reference or value type
                return false;
            }

            throw new ArgumentException();
        }

        private CLRTypeSpec TranslateToRootClass(CLRTypeSpec typeSpec)
        {
            if (typeSpec is CLRTypeSpecClass || typeSpec is CLRTypeSpecGenericInstantiation)
                return typeSpec;
            else if (typeSpec is CLRTypeSpecComplexArray || typeSpec is CLRTypeSpecSZArray)
                return m_commonTypes.Array;
            else if (typeSpec is CLRTypeSpecVarOrMVar)
            {
                CLRGenericParamRow genericParam = GenericParamForSpec((CLRTypeSpecVarOrMVar)typeSpec);

                if (genericParam.NotNullableValueTypeConstraint)
                    return m_commonTypes.ValueType;

                foreach (CLRGenericParamConstraintRow constraint in genericParam.Constraints)
                {
                    CLRTypeSpec constraintType = m_builder.Assemblies.InternTypeDefOrRefOrSpec(constraint.Constraint);
                    CppClass constraintClass = m_builder.GetCachedClass(constraintType);
                    if (constraintClass.TypeDef.Semantics == CLRTypeDefRow.TypeSemantics.Interface)
                        continue;
                    return constraintType;
                }

                return m_commonTypes.Object;
            }

            throw new ArgumentException();
        }

        public CLRTypeSpec FindCommonBase(CLRTypeSpec typeSpec1, CLRTypeSpec typeSpec2)
        {
            CLRTypeSpec trace1 = TranslateToRootClass(typeSpec1);
            CLRTypeSpec trace2 = TranslateToRootClass(typeSpec2);

            HashSet<CLRTypeSpec> root1hierarchy = new HashSet<CLRTypeSpec>();
            while (trace1 != null)
            {
                root1hierarchy.Add(trace1);
                CppClass cls = m_builder.GetCachedClass(trace1);
                trace1 = cls.ParentTypeSpec;
            }

            while (trace2 != null)
            {
                if (root1hierarchy.Contains(trace2))
                    return trace2;
                CppClass cls = m_builder.GetCachedClass(trace2);
                trace2 = cls.ParentTypeSpec;
            }

            throw new ArgumentException();
        }
    }
}
