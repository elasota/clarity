using System;
using Clarity.Rpa;

namespace Clarity.RpaCompiler
{
    public class AssignabilityResolver
    {
        private Compiler m_compiler;

        private TypeSpecClassTag m_objectType;
        private TypeSpecClassTag m_arrayType;
        private TypeNameTag m_refSZArrayName;
        private TypeNameTag m_valueSZArrayName;
        private TypeNameTag m_nullableSZArrayName;
        private TypeNameTag m_nullableName;
        private TypeNameTag m_ilistName;
        private TypeNameTag m_icollectionName;
        private TypeNameTag m_ienumerableName;

        public enum ConversionType
        {
            NotConvertible,
            Exact,
            ClassToClass,
            InterfaceToInterface,
            ClassToInterface,
            ArrayToGenericCollection,
            ArrayToGenericEnumerable,
            ArrayToGenericList,
            InterfaceToObject,
        }

        public AssignabilityResolver(Compiler compiler)
        {
            m_compiler = compiler;

            m_objectType = ResolveSimpleType("System", "Object");
            m_arrayType = ResolveSimpleType("System", "Array");
            m_refSZArrayName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "Clarity", "RefSZArray"));
            m_valueSZArrayName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "Clarity", "ValueSZArray`1", 1, null));
            m_nullableSZArrayName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "Clarity", "NullableSZArray`1", 1, null));
            m_nullableName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "System", "Nullable`1", 1, null));

            m_ilistName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "System.Collections.Generic", "IList`1", 1, null));
            m_icollectionName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "System.Collections.Generic", "ICollection`1", 1, null));
            m_ienumerableName = compiler.TagRepository.InternTypeName(new TypeNameTag("mscorlib", "System.Collections.Generic", "IEnumerable`1", 1, null));
        }

        private TypeSpecClassTag ResolveSimpleType(string typeNamespace, string typeName)
        {
            TypeNameTag typeNameTag = new TypeNameTag("mscorlib", typeNamespace, typeName, 0, null);
            typeNameTag = m_compiler.TagRepository.InternTypeName(typeNameTag);


            TypeSpecClassTag classTag = new TypeSpecClassTag(typeNameTag, new TypeSpecTag[0]);
            return (TypeSpecClassTag)m_compiler.TagRepository.InternTypeSpec(classTag);
        }

        private TypeSpecClassTag ArrayClassForSubscript(TypeSpecTag subscriptType)
        {
            if (m_compiler.TypeIsValueType(subscriptType))
            {
                TypeSpecClassTag subscriptClass = (TypeSpecClassTag)subscriptType;
                if (subscriptClass.TypeName == m_nullableName)
                {
                    if (subscriptClass.ArgTypes.Length != 1)
                        throw new RpaCompileException("Malformed Nullable");
                    TypeSpecClassTag classTag = new TypeSpecClassTag(m_nullableSZArrayName, subscriptClass.ArgTypes);
                    classTag = (TypeSpecClassTag)m_compiler.TagRepository.InternTypeSpec(classTag);

                    return classTag;
                }
                else
                {
                    TypeSpecClassTag classTag = new TypeSpecClassTag(m_valueSZArrayName, new TypeSpecTag[1] { subscriptType });
                    classTag = (TypeSpecClassTag)m_compiler.TagRepository.InternTypeSpec(classTag);

                    return classTag;
                }
            }
            else
            {
                TypeSpecClassTag classTag = new TypeSpecClassTag(m_refSZArrayName, new TypeSpecTag[0]);
                classTag = (TypeSpecClassTag)m_compiler.TagRepository.InternTypeSpec(classTag);

                return classTag;
            }
        }

        public ConversionType ResolveRefAssignable(TypeSpecTag from, TypeSpecTag to)
        {
            if (from == to)
                return ConversionType.Exact;

            if (from is TypeSpecClassTag)
            {
                TypeSpecClassTag tFrom = (TypeSpecClassTag)from;
                HighTypeDef fromTypeDef = m_compiler.GetTypeDef(tFrom.TypeName);

                if (tFrom.ArgTypes.Length != 0)
                {
                    if (fromTypeDef.Semantics == TypeSemantics.Delegate || fromTypeDef.Semantics == TypeSemantics.Interface)
                    {
                        ConversionType ct = ResolveGenericVariantAssignableTo(from, to);
                        if (ct != ConversionType.NotConvertible)
                            return ct;
                    }
                }

                if (fromTypeDef.Semantics == TypeSemantics.Interface)
                {
                    CliInterface fromIfc = m_compiler.GetClosedInterface(tFrom);
                    return ResolveInterfaceBasedOn(fromIfc, to);
                }
                else
                {
                    CliClass fromClass = m_compiler.GetClosedClass(tFrom);
                    return ResolveClassBasedOn(fromClass, to);
                }
            }
            else if (from is TypeSpecArrayTag)
            {
                TypeSpecArrayTag cplxFrom = (TypeSpecArrayTag)from;
                TypeSpecTag subscriptFrom = cplxFrom.SubscriptType;

                if (!cplxFrom.IsSZArray)
                {
                    if (to is TypeSpecClassTag)
                    {
                        TypeSpecClassTag toClass = (TypeSpecClassTag)to;
                        TypeNameTag toName = toClass.TypeName;

                        TypeSpecClassTag arrayClassType = m_arrayType;
                        if (IsReferenceType(toClass))
                            return ResolveRefAssignable(arrayClassType, toClass);

                        return ConversionType.NotConvertible;
                    }

                    if (!(to is TypeSpecArrayTag))
                        return ConversionType.NotConvertible;

                    TypeSpecArrayTag cplxTo = (TypeSpecArrayTag)to;

                    if (cplxFrom.Rank != cplxTo.Rank)
                        return ConversionType.NotConvertible;

                    for (uint i = 0; i < cplxFrom.Rank; i++)
                        if (cplxFrom.LowBounds[i] != cplxTo.LowBounds[i])
                            return ConversionType.NotConvertible;

                    TypeSpecTag subscriptTo = cplxTo.SubscriptType;

                    if (subscriptFrom == subscriptTo)
                        return ConversionType.ClassToClass;
                    if (IsReferenceType(subscriptFrom) && IsReferenceType(subscriptTo) && ResolveRefAssignable(subscriptFrom, subscriptTo) != ConversionType.NotConvertible)
                        return ConversionType.ClassToClass;
                    return ConversionType.NotConvertible;
                }
                else
                {
                    // SZArray
                    if (to is TypeSpecClassTag)
                    {
                        TypeSpecClassTag toClass = (TypeSpecClassTag)to;
                        TypeNameTag toName = toClass.TypeName;

                        if (toClass.ArgTypes.Length == 1)
                        {
                            if (toName == m_icollectionName || toName == m_ienumerableName || toName == m_ilistName)
                            {
                                TypeSpecTag ifcSubscriptTo = toClass.ArgTypes[0];
                                if (IsReferenceType(subscriptFrom) && IsReferenceType(ifcSubscriptTo) && ResolveRefAssignable(subscriptFrom, ifcSubscriptTo) != ConversionType.NotConvertible)
                                {
                                    if (toName == m_icollectionName)
                                        return ConversionType.ArrayToGenericCollection;
                                    if (toName == m_ienumerableName)
                                        return ConversionType.ArrayToGenericEnumerable;
                                    if (toName == m_ilistName)
                                        return ConversionType.ArrayToGenericList;
                                    throw new Exception();
                                }
                            }
                        }

                        TypeSpecClassTag arrayClassType = ArrayClassForSubscript(subscriptFrom);
                        if (IsReferenceType(toClass))
                        {
                            ConversionType ct = ResolveRefAssignable(arrayClassType, toClass);
                            if (ct != ConversionType.Exact)
                                return ct;
                        }

                        return ConversionType.NotConvertible;
                    }

                    if (!(to is TypeSpecArrayTag))
                        return ConversionType.NotConvertible;

                    TypeSpecArrayTag arrayFrom = (TypeSpecArrayTag)from;
                    TypeSpecArrayTag arrayTo = (TypeSpecArrayTag)to;

                    TypeSpecTag subscriptTo = arrayTo.SubscriptType;

                    if (IsReferenceType(subscriptFrom) && ResolveRefAssignable(subscriptFrom, subscriptTo) != ConversionType.NotConvertible)
                        return ConversionType.ClassToClass;
                    if (subscriptFrom == subscriptTo)
                        return ConversionType.ClassToClass;
                    return ConversionType.NotConvertible;
                }
            }

            throw new ArgumentException();
        }

        public ConversionType ResolveGenericVariantAssignableTo(TypeSpecTag from, TypeSpecTag to)
        {
            if (from == to)
                return ConversionType.Exact;

            TypeSpecClassTag fromGI = from as TypeSpecClassTag;
            TypeSpecClassTag toGI = to as TypeSpecClassTag;

            if (fromGI == null || toGI == null)
                return ConversionType.NotConvertible;

            HighTypeDef typeDef = m_compiler.GetTypeDef(fromGI.TypeName);
            if (fromGI.TypeName != toGI.TypeName)
                return ConversionType.NotConvertible;

            uint numParams = typeDef.NumGenericParameters;
            if (numParams != fromGI.ArgTypes.Length || numParams != toGI.ArgTypes.Length)
                throw new RpaCompileException("Generic parameter type mismatch");

            if (typeDef.Semantics != TypeSemantics.Interface && typeDef.Semantics != TypeSemantics.Delegate)
                return ConversionType.NotConvertible;

            for (uint i = 0; i < numParams; i++)
            {
                TypeSpecTag fromParam = fromGI.ArgTypes[i];
                TypeSpecTag toParam = fromGI.ArgTypes[i];

                if (fromParam == toParam)
                    continue;

                HighVariance variance = typeDef.GenericParameterVariance(i);
                switch (variance)
                {
                    case HighVariance.None:
                        // Invariant: Can't assign at all
                        return ConversionType.NotConvertible;
                    case HighVariance.Covariant:
                        if (!IsReferenceType(fromParam) || !IsReferenceType(toParam) || ResolveRefAssignable(fromParam, toParam) == ConversionType.NotConvertible)
                            return ConversionType.NotConvertible;
                        break;
                    case HighVariance.Contravariant:
                        if (!IsReferenceType(fromParam) || !IsReferenceType(toParam) || ResolveRefAssignable(toParam, fromParam) == ConversionType.NotConvertible)
                            return ConversionType.NotConvertible;
                        break;
                    default:
                        throw new ArgumentException();
                }
            }

            if (typeDef.Semantics == TypeSemantics.Interface)
                return ConversionType.InterfaceToInterface;
            if (typeDef.Semantics == TypeSemantics.Delegate)
                return ConversionType.ClassToClass;

            throw new ArgumentException();
        }

        private bool IsReferenceType(TypeSpecTag fromParam)
        {
            return !m_compiler.TypeIsValueType(fromParam);
        }

        private ConversionType ResolveInterfaceBasedOn(CliInterface ifc, TypeSpecTag to)
        {
            if (to == m_objectType)
                return ConversionType.InterfaceToObject;

            HighTypeDef typeDef = m_compiler.GetTypeDef(ifc.TypeSpec.TypeName);

            foreach (TypeSpecClassTag newIfc in ifc.InterfaceImpls2)
            {
                if (ResolveGenericVariantAssignableTo(newIfc, to) != ConversionType.NotConvertible)
                    return ConversionType.InterfaceToInterface;
            }

            return ConversionType.NotConvertible;
        }

        private ConversionType ResolveClassBasedOn(CliClass cls, TypeSpecTag to)
        {
            HighTypeDef typeDef = m_compiler.GetTypeDef(cls.TypeSpec.TypeName);

            foreach (CliInterfaceImpl newIfc in cls.InterfaceImpls2)
            {
                if (ResolveGenericVariantAssignableTo(newIfc.Interface, to) != ConversionType.NotConvertible)
                    return ConversionType.ClassToInterface;
            }

            if (cls.ParentClassSpec == null)
                return ConversionType.NotConvertible;

            if (cls.ParentClassSpec == to)
                return ConversionType.ClassToClass;

            return ResolveClassBasedOn(m_compiler.GetClosedClass(cls.ParentClassSpec), to);
        }
    }
}
