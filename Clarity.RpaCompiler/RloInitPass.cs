using System;
using System.Collections.Generic;
using Clarity.Rpa;
using Clarity.Rpa.Instructions;

namespace Clarity.RpaCompiler
{
    // This pass lowers all high-level RPA instructions to RLO instructions and performs validation.
    // Some things that this does not do:
    // - Validate that return instructions are not in protected regions.  This is done by the exception init pass.
    // - Validate LeaveRegion instructions.  This is done by the exception init pass.
    // - Validate that catch instructions are first-in-region and have no predecessors, that's also done by the
    //   exception linkup pass.
    // - Convert arg locals to load-from-locals, that's done by the call conversion pass.
    public class RloInitPass : RloPerNodePass
    {
        enum NumberType
        {
            Float,
            Int,
        }

        private RloFindPredecessorsAndSuccessorsPass m_psPass;
        private HighInstruction.VisitSsaDelegate m_addTypeVisitor;

        private TypeSpecClassTag m_boolType;
        private TypeSpecClassTag m_charType;
        private TypeSpecClassTag m_int8Type;
        private TypeSpecClassTag m_int16Type;
        private TypeSpecClassTag m_int32Type;
        private TypeSpecClassTag m_int64Type;
        private TypeSpecClassTag m_uint8Type;
        private TypeSpecClassTag m_uint16Type;
        private TypeSpecClassTag m_uint32Type;
        private TypeSpecClassTag m_uint64Type;
        private TypeSpecClassTag m_float32Type;
        private TypeSpecClassTag m_float64Type;
        private TypeSpecClassTag m_nativeIntType;
        private TypeSpecClassTag m_nativeUIntType;
        private TypeSpecClassTag m_objectType;
        private TypeSpecClassTag m_runtimeTypeHandleType;
        private TypeSpecClassTag m_runtimeFieldHandleType;

        private Dictionary<TypeSpecTag, TypeSpecClassTag> m_simplifiedNumberType;

        public RloInitPass(Compiler compiler, RloMethodBody methodBody, RloFindPredecessorsAndSuccessorsPass psPass)
            : base(compiler, methodBody)
        {
            m_psPass = psPass;
            m_addTypeVisitor = AddSsaTypes;

            TagRepository repo = compiler.TagRepository;

            m_boolType = GetBuiltinType(repo, "System", "Boolean");
            m_charType = GetBuiltinType(repo, "System", "Char");
            m_int8Type = GetBuiltinType(repo, "System", "SByte");
            m_int16Type = GetBuiltinType(repo, "System", "Int16");
            m_int32Type = GetBuiltinType(repo, "System", "Int32");
            m_int64Type = GetBuiltinType(repo, "System", "Int64");
            m_uint8Type = GetBuiltinType(repo, "System", "Byte");
            m_uint16Type = GetBuiltinType(repo, "System", "UInt16");
            m_uint32Type = GetBuiltinType(repo, "System", "UInt32");
            m_uint64Type = GetBuiltinType(repo, "System", "UInt64");
            m_float32Type = GetBuiltinType(repo, "System", "Single");
            m_float64Type = GetBuiltinType(repo, "System", "Double");
            m_nativeIntType = GetBuiltinType(repo, "System", "IntPtr");
            m_nativeUIntType = GetBuiltinType(repo, "System", "UIntPtr");
            m_objectType = GetBuiltinType(repo, "System", "Object");
            m_runtimeTypeHandleType = GetBuiltinType(repo, "System", "RuntimeTypeHandle");
            m_runtimeFieldHandleType = GetBuiltinType(repo, "System", "RuntimeFieldHandle");

            m_simplifiedNumberType = new Dictionary<TypeSpecTag, TypeSpecClassTag>();
            m_simplifiedNumberType.Add(m_boolType, m_int32Type);
            m_simplifiedNumberType.Add(m_charType, m_int32Type);
            m_simplifiedNumberType.Add(m_int8Type, m_int32Type);
            m_simplifiedNumberType.Add(m_int16Type, m_int32Type);
            m_simplifiedNumberType.Add(m_int32Type, m_int32Type);
            m_simplifiedNumberType.Add(m_int64Type, m_int64Type);
            m_simplifiedNumberType.Add(m_uint8Type, m_int32Type);
            m_simplifiedNumberType.Add(m_uint16Type, m_int32Type);
            m_simplifiedNumberType.Add(m_uint32Type, m_int32Type);
            m_simplifiedNumberType.Add(m_uint64Type, m_int64Type);
            m_simplifiedNumberType.Add(m_nativeIntType, m_nativeIntType);
            m_simplifiedNumberType.Add(m_nativeUIntType, m_nativeIntType);
            m_simplifiedNumberType.Add(m_float32Type, m_float64Type);
            m_simplifiedNumberType.Add(m_float64Type, m_float64Type);
        }

        private MethodInstantiationPath GenerateMethodInstantiationPath(CodeLocationTag codeLocation)
        {
            return new MethodInstantiationPath(this.MethodBody.InstantiationPath, this.MethodBody.MethodSpec, codeLocation);
        }

        private TypeSpecClassTag GetBuiltinType(TagRepository repo, string typeNamespace, string typeName)
        {
            TypeNameTag typeNameTag = repo.InternTypeName(new TypeNameTag("mscorlib", typeNamespace, typeName, null));
            TypeSpecTag typeSpecTag = repo.InternTypeSpec(new TypeSpecClassTag(typeNameTag, new TypeSpecTag[0]));

            return (TypeSpecClassTag)typeSpecTag;
        }

        private void AddSsaTypes(ref HighSsaRegister ssaRegRef)
        {
            HighSsaRegister ssaReg = ssaRegRef;
            if (ssaReg == null)
                return;
            TypeSpecTag type = ssaReg.Type;
            if (type != null && type is TypeSpecClassTag)
            {
                TypeSpecClassTag typeSpecClass = (TypeSpecClassTag)type;
                switch (this.Compiler.GetTypeDef(typeSpecClass.TypeName).Semantics)
                {
                    case TypeSemantics.Class:
                    case TypeSemantics.Delegate:
                    case TypeSemantics.Enum:
                    case TypeSemantics.Struct:
                        this.Compiler.GetClosedClass(typeSpecClass);
                        break;
                    case TypeSemantics.Interface:
                        this.Compiler.GetClosedInterface(typeSpecClass);
                        break;
                    default:
                        throw new RpaCompileException("Unknown type semantics");
                        break;
                }
            }
        }

        private static bool CanPhiMatchVT(HighValueType phiVT, HighValueType destVT)
        {
            switch (phiVT)
            {
                case HighValueType.ConstantString:
                case HighValueType.Null:
                case HighValueType.ReferenceValue:
                    return destVT == HighValueType.ReferenceValue;
                case HighValueType.ConstantValue:
                case HighValueType.ValueValue:
                    return destVT == HighValueType.ValueValue;
                case HighValueType.ManagedPtr:
                    return destVT == HighValueType.ManagedPtr;
                default:
                    throw new Exception();
            }
        }

        protected override void ProcessNode(HighCfgNode cfgNode)
        {
            int numInstructions = cfgNode.Instructions.Length;
            if (cfgNode.Instructions.Length == 0)
                throw new Exception("CFG node has no instructions");

            HighInstruction[] instructions = cfgNode.Instructions;
            for (int i = 0; i < numInstructions; i++)
                if (instructions[i].TerminatesControlFlow != (i == numInstructions - 1))
                    throw new Exception("Unexpected control flow instruction location");

            // Validate phis
            ISet<HighCfgNode> nodePreds = m_psPass.PredecessorsForNode(cfgNode);

            foreach (HighPhi phi in cfgNode.Phis)
            {
                HashSet<HighCfgNode> linkPreds = new HashSet<HighCfgNode>();
                foreach (HighPhiLink link in phi.Links)
                {
                    if (!CanPhiMatchVT(link.Reg.ValueType, phi.Dest.ValueType) || link.Reg.Type != phi.Dest.Type)
                        throw new Exception("Phi predecessor type does not match link destination type");
                    if (!linkPreds.Add(link.Predecessor.Value))
                        throw new Exception("Duplicate phi predecessor");
                }

                // It's OK for a phi link predecessor to not be an actual predecessor, but it's not
                // OK for an actual predecessor to be missing a phi link
                if (nodePreds != null)
                {
                    foreach (HighCfgNode nodePred in nodePreds)
                        if (!linkPreds.Contains(nodePred))
                            throw new Exception("Phi is missing a predecessor link");
                }
            }

            // Process instructions
            List<HighInstruction> newInstrs = new List<HighInstruction>();
            foreach (HighInstruction instr in cfgNode.Instructions)
                ProcessInstruction(cfgNode, instr, newInstrs);

            // Add SSA types
            foreach (HighPhi phi in cfgNode.Phis)
            {
                phi.VisitSsaDests(m_addTypeVisitor);
                phi.VisitSsaUses(m_addTypeVisitor);
            }

            foreach (HighInstruction instr in cfgNode.Instructions)
            {
                instr.VisitSsaDests(m_addTypeVisitor);
                instr.VisitSsaUses(m_addTypeVisitor);
            }
        }

        private static void CheckArithDest(HighSsaRegister reg, TypeSpecClassTag expectedType)
        {
            if (reg == null)
                return;
            if (reg.ValueType != HighValueType.ValueValue)
                throw new RpaCompileException("Unexpected value type in arith instruction");
            if (reg.Type != expectedType)
                throw new RpaCompileException("Unexpected operand type in arith instruction");
        }

        private static void CheckArithOperand(HighSsaRegister reg, TypeSpecClassTag expectedType)
        {
            if (reg.ValueType != HighValueType.ConstantValue && reg.ValueType != HighValueType.ValueValue)
                throw new RpaCompileException("Unexpected value type in arith instruction");
            if (reg.Type != expectedType)
                throw new RpaCompileException("Unexpected operand type in arith instruction");
        }

        private void CheckMethodCall(MethodSpecTag methodSpec, HighSsaRegister dest, HighSsaRegister[] parameters, MethodSignatureTag methodSignature)
        {
            if (methodSpec.GenericParameters.Length != methodSignature.NumGenericParameters)
                throw new RpaCompileException("MethodSpec generic parameter count does not match method signature parameter count");

            MethodSignatureTag instSignature = methodSignature.Instantiate(this.Compiler.TagRepository, new TypeSpecTag[0], methodSpec.GenericParameters);

            MethodSignatureParam[] sigParams = instSignature.ParamTypes;
            if (sigParams.Length != parameters.Length)
                throw new RpaCompileException("MethodSpec parameter count does not match");

            int numParams = parameters.Length;
            for (int i = 0; i < numParams; i++)
            {
                MethodSignatureParam sigParam = sigParams[i];
                HighSsaRegister param = parameters[i];

                switch (sigParam.TypeOfType.Value)
                {
                    case MethodSignatureParamTypeOfType.Values.TypedByRef:
                        throw new NotSupportedException();
                    case MethodSignatureParamTypeOfType.Values.ByRef:
                        {
                            if (param.ValueType != HighValueType.ManagedPtr)
                                throw new RpaCompileException("ByRef parameter was not passed as a managed pointer");
                        }
                        break;
                    case MethodSignatureParamTypeOfType.Values.Value:
                        {
                            switch (param.ValueType)
                            {
                                case HighValueType.ConstantString:
                                case HighValueType.ConstantValue:
                                case HighValueType.ReferenceValue:
                                case HighValueType.ValueValue:
                                    // These are always OK if the type matches
                                    break;
                                case HighValueType.Null:
                                    // These are OK as long as the type matches and the type is not a value type
                                    if (this.Compiler.TypeIsValueType(param.Type))
                                        throw new RpaCompileException("Value parameter was null, but a value type");
                                    break;
                                default:
                                    throw new RpaCompileException("Value parameter was not passed as a value");
                            }
                        }
                        break;
                    default:
                        throw new ArgumentException();
                }

                if (param.Type != sigParam.Type)
                    throw new RpaCompileException("Method parameter type mismatch");
            }

            if (dest != null)
            {
                switch (dest.ValueType)
                {
                    case HighValueType.ReferenceValue:
                    case HighValueType.ValueValue:
                        break;
                    default:
                        throw new RpaCompileException("Invalid method return value type");
                }
                if (instSignature.RetType != dest.Type)
                    throw new RpaCompileException("Method return type does not match");
            }
        }

        private void ProcessInstruction(HighCfgNode cfgNode, HighInstruction instr, List<HighInstruction> newInstrs)
        {
            bool validationOnly = true;

            switch (instr.Opcode)
            {
                case HighInstruction.Opcodes.LoadLocal:
                    {
                        LoadLocalInstruction tInstr = (LoadLocalInstruction)instr;
                        HighSsaRegister dest = tInstr.Dest;
                        HighLocal local = tInstr.Local;

                        switch (local.TypeOfType)
                        {
                            case HighLocal.ETypeOfType.ByRef:
                                if (dest.ValueType != HighValueType.ManagedPtr)
                                    throw new RpaCompileException("Illegal LoadLocal");
                                break;
                            case HighLocal.ETypeOfType.TypedByRef:
                                throw new NotImplementedException();
                            case HighLocal.ETypeOfType.Value:
                                if (dest.ValueType != HighValueType.ValueValue && dest.ValueType != HighValueType.ReferenceValue)
                                    throw new RpaCompileException("Illegal LoadLocal");
                                break;
                            default:
                                throw new Exception();
                        }

                        if (dest.Type != local.Type)
                            throw new RpaCompileException("Type mismatch in LoadLocal");
                    }
                    break;
                case HighInstruction.Opcodes.AllocArray:
                    {
                        AllocArrayInstruction tInstr = (AllocArrayInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest == null)
                            throw new RpaCompileException("AllocArray has no destination");
                        if (dest.ValueType != HighValueType.ReferenceValue || !(dest.Type is TypeSpecArrayTag))
                            throw new RpaCompileException("AllocArray destination is not an array");
                        TypeSpecArrayTag type = (TypeSpecArrayTag)dest.Type;
                        if (type.Rank != (uint)tInstr.Sizes.Length)
                            throw new RpaCompileException("AllocArray index count doesn't match destination type rank");
                        
                        foreach (HighSsaRegister sz in tInstr.Sizes)
                        {
                            switch (sz.ValueType)
                            {
                                case HighValueType.ConstantValue:
                                case HighValueType.ValueValue:
                                    break;
                                default:
                                    throw new RpaCompileException("AllocArray index is invalid");
                            }

                            if (sz.Type != m_nativeIntType)
                                throw new RpaCompileException("AllocArray index is invalid");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.AllocObj:
                    {
                        AllocObjInstruction tInstr = (AllocObjInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest == null)
                            throw new RpaCompileException("AllocObj has no destination");

                        if (dest.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("AllocObj destination is not a reference type");

                        TypeSpecClassTag destType = dest.Type as TypeSpecClassTag;
                        if (destType == null)
                            throw new RpaCompileException("AllocObj destination type is not a class");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(destType.TypeName);
                        if (typeDef.Semantics != TypeSemantics.Class)
                            throw new RpaCompileException("AllocObj created non-class");
                        if (typeDef.IsAbstract)
                            throw new RpaCompileException("AllocObj created class is abstract");
                    }
                    break;
                case HighInstruction.Opcodes.Box:
                    {
                        BoxInstruction tInstr = (BoxInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        HighSsaRegister src = tInstr.Src;
                        if (dest == null)
                            throw new RpaCompileException("Box has no destination");


                        if ((src.ValueType != HighValueType.ConstantValue || src.ValueType != HighValueType.ValueValue) && dest.ValueType == HighValueType.BoxedValue)
                        {
                            // Normally don't do anything, leave box as canonical
                            // In the case of Nullable`1 only, convert the box instruction
                            if (dest.Type != src.Type)
                            {
                                if (!(src.Type is TypeSpecClassTag))
                                    throw new RpaCompileException("Invalid box source type");
                                TypeSpecClassTag srcClass = (TypeSpecClassTag)src.Type;
                                TypeNameTag srcClassName = srcClass.TypeName;
                                if (srcClassName.ContainerType != null || srcClassName.AssemblyName != "mscorlib" || srcClassName.TypeNamespace != "System" || srcClassName.TypeName != "Nullable`1")
                                    throw new RpaCompileException("Invalid box source type");
                                TypeSpecTag srcSubType = srcClass.ArgTypes[0];


                                if (dest.Type != srcSubType)
                                    throw new RpaCompileException("Nullable box type mixmatch");

                                validationOnly = false;
                                newInstrs.Add(new Instructions.BoxNullableInstruction(tInstr.CodeLocation, dest, src));
                            }
                        }
                        else if (src.ValueType == HighValueType.ReferenceValue && dest.ValueType == HighValueType.ReferenceValue)
                        {
                            if (dest.Type != src.Type)
                                throw new RpaCompileException("Box instruction destination is a different type from source");

                            // Source should never be ConstantString at this stage.
                            // Boxing a reference type converts to a copy
                            validationOnly = false;
                            newInstrs.Add(new Instructions.CopyInstruction(tInstr.CodeLocation, dest, src));
                        }
                    }
                    break;
                case HighInstruction.Opcodes.Arith:
                    {
                        ArithInstruction tInstr = (ArithInstruction)instr;
                        TypeSpecClassTag expectedClass = ExpectedClassForArithType(tInstr.ArithType);

                        if (tInstr.CheckOverflow)
                        {
                            switch (tInstr.ArithType)
                            {
                                case NumberArithType.Int32:
                                case NumberArithType.Int64:
                                case NumberArithType.NativeInt:
                                case NumberArithType.NativeUInt:
                                case NumberArithType.UInt32:
                                case NumberArithType.UInt64:
                                    break;
                                case NumberArithType.Float32:
                                case NumberArithType.Float64:
                                    throw new RpaCompileException("Check overflow flag on flowing point arith operation");
                                default:
                                    throw new Exception();
                            }
                        }

                        CheckArithDest(tInstr.Dest, expectedClass);
                        CheckArithOperand(tInstr.Left, expectedClass);
                        CheckArithOperand(tInstr.Right, expectedClass);
                    }
                    break;
                case HighInstruction.Opcodes.BranchCompareNumbers:
                    {
                        BranchCompareNumbersInstruction tInstr = (BranchCompareNumbersInstruction)instr;

                        TypeSpecClassTag expectedClass = ExpectedClassForArithType(tInstr.ArithType);

                        CheckArithOperand(tInstr.Left, expectedClass);
                        CheckArithOperand(tInstr.Right, expectedClass);
                    }
                    break;
                case HighInstruction.Opcodes.DynamicCast:
                    {
                        DynamicCastInstruction tInstr = (DynamicCastInstruction)instr;

                        if (tInstr.Dest != null)
                        {
                            switch (tInstr.Dest.ValueType)
                            {
                                case HighValueType.BoxedValue:
                                case HighValueType.ReferenceValue:
                                    break;
                                default:
                                    throw new RpaCompileException("Illegal destination type for dynamic cast instruction");
                            }
                        }

                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("Illegal source type for dynamic cast instruction.");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.ForceDynamicCast:
                    {
                        ForceDynamicCastInstruction tInstr = (ForceDynamicCastInstruction)instr;

                        if (tInstr.Dest != null)
                        {
                            switch (tInstr.Dest.ValueType)
                            {
                                case HighValueType.BoxedValue:
                                case HighValueType.ReferenceValue:
                                    break;
                                default:
                                    throw new RpaCompileException("Illegal destination type for force dynamic cast instruction");
                            }
                        }

                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("Illegal source type for force dynamic cast instruction.");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.GetArrayElementPtr:
                    {
                        GetArrayElementPtrInstruction tInstr = (GetArrayElementPtrInstruction)instr;

                        HighSsaRegister arraySrc = tInstr.ArraySrc;
                        if (arraySrc.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("GetArrayElementPtr instruction array is not a reference");

                        TypeSpecArrayTag arrayType = arraySrc.Type as TypeSpecArrayTag;
                        if (arrayType == null)
                            throw new RpaCompileException("GetArrayElementPtr instruction arrays source is not an array");

                        if ((uint)tInstr.Indexes.Length != arrayType.Rank)
                            throw new RpaCompileException("GetArrayElementPtr instruction array source rank doesn't match index count");

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest != null)
                        {
                            if (dest.ValueType != HighValueType.ManagedPtr)
                                throw new RpaCompileException("GetArrayElementPtr destination is not a managed pointer");
                            if (dest.Type != arrayType.SubscriptType)
                                throw new RpaCompileException("GetArrayElementPtr destination does not match subscript type");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.CompareRefs:
                    {
                        CompareRefsInstruction tInstr = (CompareRefsInstruction)instr;
                        HighSsaRegister[] sources = new HighSsaRegister[2];
                        sources[0] = tInstr.SrcA;
                        sources[1] = tInstr.SrcB;

                        if (tInstr.Dest != null && tInstr.Dest.ValueType != HighValueType.ValueValue && tInstr.Dest.Type != m_int32Type)
                            throw new RpaCompileException("CompareRefs destination is not an int");

                        bool isSideConverted = false;
                        UpdateRefCompare(tInstr.CodeLocation, sources, newInstrs, out isSideConverted);

                        if (isSideConverted)
                        {
                            validationOnly = false;
                            newInstrs.Add(new CompareRefsInstruction(tInstr.CodeLocation, tInstr.Dest, sources[0], sources[1], tInstr.EqualValue, tInstr.NotEqualValue));
                        }
                    }
                    break;
                case HighInstruction.Opcodes.BranchCompareRefs:
                    {
                        BranchCompareRefsInstruction tInstr = (BranchCompareRefsInstruction)instr;
                        HighSsaRegister[] sources = new HighSsaRegister[2];
                        sources[0] = tInstr.SrcA;
                        sources[1] = tInstr.SrcB;

                        bool isSideConverted = false;
                        UpdateRefCompare(tInstr.CodeLocation, sources, newInstrs, out isSideConverted);

                        if (isSideConverted)
                        {
                            validationOnly = false;
                            newInstrs.Add(new BranchCompareRefsInstruction(tInstr.CodeLocation, sources[0], sources[1], tInstr.EqualEdge.Dest, tInstr.NotEqualEdge.Dest));
                        }
                    }
                    break;
                case HighInstruction.Opcodes.GetStaticFieldAddr:
                    {
                        GetStaticFieldAddrInstruction tInstr = (GetStaticFieldAddrInstruction)instr;

                        TypeSpecClassTag classSpec = tInstr.StaticType as TypeSpecClassTag;
                        if (classSpec == null)
                            throw new RpaCompileException("GetStaticFieldAddr type is not a class");
                        CliClass cls = this.Compiler.GetClosedClass(classSpec);

                        uint fieldIndex;
                        if (!cls.NameToStaticFieldSlot.TryGetValue(tInstr.FieldName, out fieldIndex))
                            throw new RpaCompileException("GetStaticFieldAddr could not match static field name");

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest != null)
                        {
                            if (dest.ValueType != HighValueType.ManagedPtr)
                                throw new RpaCompileException("GetStaticFieldAddr dest is not a managed pointer");
                            if (dest.Type != cls.StaticFields[fieldIndex].Type)
                                throw new RpaCompileException("GetStaticFieldAddr dest type does not match field type");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.BranchRefNull:
                    {
                        BranchRefNullInstruction tInstr = (BranchRefNullInstruction)instr;

                        HighSsaRegister src = tInstr.Src;
                        switch (src.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("BranchRefNull source is not a reference");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.GetTypeInfo:
                    {
                        GetTypeInfoInstruction tInstr = (GetTypeInfoInstruction)instr;

                        if (tInstr.Dest.ValueType != HighValueType.ValueValue || tInstr.Dest.Type != m_runtimeTypeHandleType)
                            throw new RpaCompileException("GetTypeInfo destination is not the correct type");
                    }
                    break;
                case HighInstruction.Opcodes.LoadPtr:
                    {
                        LoadPtrInstruction tInstr = (LoadPtrInstruction)instr;

                        if (tInstr.Dest != null)
                        {
                            switch (tInstr.Dest.ValueType)
                            {
                                case HighValueType.ReferenceValue:
                                case HighValueType.ValueValue:
                                    break;
                                default:
                                    throw new RpaCompileException("LoadPtr destination has an invalid value type");
                            }
                        }

                        if (tInstr.Src.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("LoadPtr source is not a managed pointer");

                        if (tInstr.Src.Type != tInstr.Dest.Type)
                            throw new RpaCompileException("LoadPtr source type is not the same as dest type");
                    }
                    break;
                case HighInstruction.Opcodes.PtrField:
                    {
                        PtrFieldInstruction tInstr = (PtrFieldInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        HighSsaRegister src = tInstr.Src;

                        if (src.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("PtrField source not a field");

                        TypeSpecClassTag classSpec = src.Type as TypeSpecClassTag;
                        if (classSpec == null)
                            throw new RpaCompileException("PtrField source is not a class");

                        CliClass cls = this.Compiler.GetClosedClass(classSpec);
                        uint fieldIndex;
                        if (!cls.NameToInstanceFieldSlot.TryGetValue(tInstr.FieldName, out fieldIndex))
                            throw new RpaCompileException("PtrField field does not exist");
                        
                        if (dest != null)
                        {
                            if (dest.ValueType != HighValueType.ManagedPtr)
                                throw new RpaCompileException("PtrField dest is not a field");
                            HighField fld = cls.InstanceFields[fieldIndex];
                            if (fld.Type != dest.Type)
                                throw new RpaCompileException("PtrField dest type does not match field type");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.RefField:
                    {
                        RefFieldInstruction tInstr = (RefFieldInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        HighSsaRegister src = tInstr.Src;

                        if (src.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("RefField source not a field");

                        TypeSpecClassTag classSpec = src.Type as TypeSpecClassTag;
                        if (classSpec == null)
                            throw new RpaCompileException("RefField source is not a class");

                        CliClass cls = this.Compiler.GetClosedClass(classSpec);
                        uint fieldIndex;
                        if (!cls.NameToInstanceFieldSlot.TryGetValue(tInstr.FieldName, out fieldIndex))
                            throw new RpaCompileException("RefField field does not exist");

                        if (dest != null)
                        {
                            if (dest.ValueType != HighValueType.ManagedPtr)
                                throw new RpaCompileException("RefField dest is not a field");
                            HighField fld = cls.InstanceFields[fieldIndex];
                            if (fld.Type != dest.Type)
                                throw new RpaCompileException("RefField dest type does not match field type");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.CallInstanceMethod:
                    {
                        CallInstanceMethodInstruction tInstr = (CallInstanceMethodInstruction)instr;
                        HighSsaRegister instance = tInstr.InstanceSrc;
                        HighSsaRegister[] parameters = tInstr.Parameters;
                        HighSsaRegister dest = tInstr.ReturnDest;

                        TypeSpecClassTag classSpec = tInstr.MethodSpec.DeclaringClass;

                        if (instance.Type != tInstr.MethodSpec.DeclaringClass)
                            throw new RpaCompileException("CallInstanceMethod target is not the same class as the method being called");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(classSpec.TypeName);

                        switch (typeDef.Semantics)
                        {
                            case TypeSemantics.Interface:
                                throw new RpaCompileException("CallInstanceMethod target is an interface");
                            case TypeSemantics.Class:
                            case TypeSemantics.Delegate:
                            case TypeSemantics.Enum:
                            case TypeSemantics.Struct:
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        switch (instance.ValueType)
                        {
                            case HighValueType.ConstantString:
                            case HighValueType.ReferenceValue:
                            case HighValueType.ManagedPtr:
                                break;

                            // BoxedValues should never have methods called on them directly (use unbox first)
                            // Calls on null at this stage are illegal
                            default:
                                throw new RpaCompileException("CallInstanceMethod source type is invalid");
                        }

                        CliClass cliClass = this.Compiler.GetClosedClass(classSpec);
                        MethodSpecTag methodSpec = tInstr.MethodSpec;
                        if (methodSpec.MethodSlotType != MethodSlotType.Instance)
                            throw new RpaCompileException("CallInstanceMethod method is not an instance method");

                        uint methodSlot;
                        if (!cliClass.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodSlot))
                            throw new RpaCompileException("CallInstanceMethod method wasn't found");
                        HighMethod method = cliClass.Methods[methodSlot];
                        if (method.IsStatic)
                            throw new RpaCompileException("CallInstanceMethod method is static");

                        CheckMethodCall(methodSpec, dest, parameters, method.MethodSignature);

                        MethodHandle methodHandle = this.Compiler.InstantiateMethod(methodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                        validationOnly = false;
                        newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, tInstr.ReturnDest, tInstr.InstanceSrc, tInstr.Parameters));
                    }
                    break;
                case HighInstruction.Opcodes.CallStaticMethod:
                    {
                        CallStaticMethodInstruction tInstr = (CallStaticMethodInstruction)instr;
                        HighSsaRegister[] parameters = tInstr.Parameters;
                        HighSsaRegister dest = tInstr.ReturnDest;

                        TypeSpecClassTag classSpec = tInstr.MethodSpec.DeclaringClass;

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(classSpec.TypeName);

                        switch (typeDef.Semantics)
                        {
                            case TypeSemantics.Interface:
                                throw new RpaCompileException("CallStaticMethod target is an interface");
                            case TypeSemantics.Class:
                            case TypeSemantics.Delegate:
                            case TypeSemantics.Enum:
                            case TypeSemantics.Struct:
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        CliClass cliClass = this.Compiler.GetClosedClass(classSpec);
                        MethodSpecTag methodSpec = tInstr.MethodSpec;
                        if (methodSpec.MethodSlotType != MethodSlotType.Static)
                            throw new RpaCompileException("CallStaticMethod method is not an instance method");

                        uint methodSlot;
                        if (!cliClass.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodSlot))
                            throw new RpaCompileException("CallStaticMethod method wasn't found");
                        HighMethod method = cliClass.Methods[methodSlot];
                        if (!method.IsStatic)
                            throw new RpaCompileException("CallStaticMethod method is not static");

                        CheckMethodCall(methodSpec, dest, parameters, method.MethodSignature);

                        MethodHandle methodHandle = this.Compiler.InstantiateMethod(methodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                        validationOnly = false;
                        newInstrs.Add(new Instructions.CallRloStaticMethodInstruction(tInstr.CodeLocation, methodHandle, tInstr.ReturnDest, tInstr.Parameters));
                    }
                    break;
                case HighInstruction.Opcodes.CallVirtualMethod:
                    {
                        CallVirtualMethodInstruction tInstr = (CallVirtualMethodInstruction)instr;
                        HighSsaRegister instance = tInstr.InstanceSrc;
                        HighSsaRegister[] parameters = tInstr.Parameters;
                        HighSsaRegister dest = tInstr.ReturnDest;
                        MethodSpecTag methodSpec = tInstr.MethodSpec;

                        if (methodSpec.MethodSlotType != MethodSlotType.Virtual)
                            throw new RpaCompileException("CallVirtualMethod target is not virtual");

                        TypeSpecClassTag classSpec = methodSpec.DeclaringClass;

                        if (instance.Type != tInstr.MethodSpec.DeclaringClass)
                            throw new RpaCompileException("CallVirtualMethod target is not the same class as the method being called");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(classSpec.TypeName);

                        bool isInterface;
                        switch (typeDef.Semantics)
                        {
                            case TypeSemantics.Interface:
                                isInterface = true;
                                break;
                            case TypeSemantics.Class:
                            case TypeSemantics.Delegate:
                            case TypeSemantics.Enum:
                            case TypeSemantics.Struct:
                                isInterface = false;
                                break;
                            default:
                                throw new ArgumentException();
                        }

                        switch (instance.ValueType)
                        {
                            case HighValueType.ConstantString:
                            case HighValueType.ReferenceValue:
                            case HighValueType.ManagedPtr:
                                break;

                            // BoxedValues should never have methods called on them directly (use unbox first)
                            // Calls on null at this stage are illegal
                            default:
                                throw new RpaCompileException("CallVirtualMethod source type is invalid");
                        }

                        if (methodSpec.GenericParameters.Length != 0)
                            throw new RpaCompileException("Can't call an unconstrained generic virtual method");

                        CliVtableSlot vtableSlot;
                        uint vtableSlotIndex;
                        MethodSignatureTag methodSignature;

                        if (isInterface)
                        {
                            CliInterface ifc = this.Compiler.GetClosedInterface(classSpec);
                            vtableSlotIndex = ifc.CliSlotForSlotTag(methodSpec.MethodDecl);
                            methodSignature = ifc.Slots[vtableSlotIndex].Signature;
                        }
                        else
                        {
                            CliClass cliClass = this.Compiler.GetClosedClass(classSpec);
                            if (!cliClass.DeclTagToVTableSlot.TryGetValue(methodSpec.MethodDecl, out vtableSlotIndex))
                                throw new RpaCompileException("CallVirtualMethod method wasn't found");
                            methodSignature = cliClass.VTable[vtableSlotIndex].MethodSignature;
                        }

                        if (methodSpec.MethodSlotType != MethodSlotType.Virtual)
                            throw new RpaCompileException("CallVirtualMethod method is not an instance method");

                        CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                        validationOnly = false;
                        if (isInterface)
                            newInstrs.Add(new Instructions.CallRloVirtualMethodInstruction(tInstr.CodeLocation, vtableSlotIndex, tInstr.ReturnDest, tInstr.InstanceSrc, tInstr.Parameters));
                        else
                            newInstrs.Add(new Instructions.CallRloInterfaceMethodInstruction(tInstr.CodeLocation, vtableSlotIndex, tInstr.ReturnDest, tInstr.InstanceSrc, tInstr.Parameters));
                    }
                    break;

                case HighInstruction.Opcodes.CallConstrainedVirtualMethod:
                    {
                        validationOnly = false;

                        CallConstrainedVirtualMethodInstruction tInstr = (CallConstrainedVirtualMethodInstruction)instr;
                        HighSsaRegister refInstance = tInstr.InstanceSrc;
                        HighSsaRegister[] parameters = tInstr.Parameters;
                        HighSsaRegister dest = tInstr.ReturnDest;
                        MethodSpecTag methodSpec = tInstr.MethodSpec;

                        if (methodSpec.MethodSlotType != MethodSlotType.Virtual)
                            throw new RpaCompileException("CallConstrainedVirtualMethod target is not virtual");

                        TypeSpecTag constraintType = tInstr.ConstraintType;

                        if (refInstance.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("CallConstrainedVirtualMethod target is not a managed pointer");

                        if (refInstance.Type != constraintType)
                            throw new RpaCompileException("CallConstrainedVirtualMethod target type is different from constraint type");

                        TypeSpecTag instanceType = refInstance.Type;

                        bool isValueType;
                        bool isInterface;

                        switch (instanceType.SubType)
                        {
                            case TypeSpecTag.SubTypeCode.Array:
                                isValueType = false;
                                isInterface = false;
                                break;
                            case TypeSpecTag.SubTypeCode.Class:
                                {
                                    TypeSpecClassTag instanceClassTag = (TypeSpecClassTag)instanceType;
                                    HighTypeDef instanceTypeDef = this.Compiler.GetTypeDef(instanceClassTag.TypeName);

                                    switch (instanceTypeDef.Semantics)
                                    {
                                        case TypeSemantics.Class:
                                        case TypeSemantics.Delegate:
                                            isValueType = false;
                                            isInterface = false;
                                            break;
                                        case TypeSemantics.Interface:
                                            isValueType = false;
                                            isInterface = true;
                                            break;
                                        case TypeSemantics.Enum:
                                        case TypeSemantics.Struct:
                                            isValueType = true;
                                            isInterface = false;
                                            break;
                                        default:
                                            throw new NotSupportedException();
                                    }
                                }
                                break;
                            default:
                                throw new RpaCompileException("Invalid instance type in CallConstrainedVirtualMethod");
                        };

                        if (isValueType)
                        {
                            TypeSpecClassTag instanceClassTag = (TypeSpecClassTag)refInstance.Type;
                            CliClass cls = this.Compiler.GetClosedClass(instanceClassTag);

                            TypeSpecClassTag methodDeclaringClass = methodSpec.DeclaringClass;

                            HighTypeDef methodTypeDef = this.Compiler.GetTypeDef(methodDeclaringClass.TypeName);

                            HighMethod resolvedMethod;
                            uint vtableSlotIndex;
                            if (methodTypeDef.Semantics != TypeSemantics.Interface)
                            {
                                if (!cls.DeclTagToVTableSlot.TryGetValue(methodSpec.MethodDecl, out vtableSlotIndex))
                                    throw new RpaCompileException("CallConstrainedVirtualMethod virtual method was not found");
                            }
                            else
                            {
                                CliInterface ifc = this.Compiler.GetClosedInterface(methodDeclaringClass);
                                uint ifcSlot = ifc.CliSlotForSlotTag(methodSpec.MethodDecl);

                                vtableSlotIndex = this.Compiler.DevirtualizeInterfaceMethod(cls, methodDeclaringClass, ifcSlot);
                            }

                            CliMethodIndex methodIndex = cls.VTable[vtableSlotIndex].MethodIndex;

                            if (methodIndex == null)
                                throw new Exception("Invalid method index (???)");

                            uint depth = methodIndex.Depth;

                            if (depth == 0)
                            {
                                resolvedMethod = cls.Methods[methodIndex.Index];

                                if (resolvedMethod.MethodSignature.NumGenericParameters != 0)
                                    throw new NotImplementedException();

                                if (cls.TypeSpec != instanceType)
                                    throw new RpaCompileException("CallConstrainedVirtualMethod instance type doesn't match method (???)");

                                int numParameters = parameters.Length;
                                HighSsaRegister tempDest = null;
                                HighSsaRegister[] tempParameters = new HighSsaRegister[numParameters];

                                MethodSignatureTag signature = resolvedMethod.MethodSignature;

                                if (signature.ParamTypes.Length != numParameters)
                                    throw new RpaCompileException("CallConstrainedVirtualMethod method call parameter mismatch");

                                for (int i = 0; i < numParameters; i++)
                                {
                                    HighValueType hvt;
                                    MethodSignatureParam sigParam = signature.ParamTypes[i];
                                    switch (sigParam.TypeOfType.Value)
                                    {
                                        case MethodSignatureParamTypeOfType.Values.ByRef:
                                            hvt = HighValueType.ManagedPtr;
                                            break;
                                        case MethodSignatureParamTypeOfType.Values.Value:
                                            hvt = this.Compiler.TypeIsValueType(sigParam.Type) ? HighValueType.ValueValue : HighValueType.ReferenceValue;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    if (hvt == HighValueType.ReferenceValue)
                                    {
                                        HighSsaRegister tempParam = new HighSsaRegister(hvt, sigParam.Type, null);
                                        EmitPassiveRefConversion(tInstr.CodeLocation, tempParam, parameters[i], newInstrs);

                                        tempParameters[i] = tempParam;
                                    }
                                    else
                                        tempParameters[i] = parameters[i];
                                }

                                HighSsaRegister returnDest = tInstr.ReturnDest;
                                HighSsaRegister tempReturn = returnDest;

                                bool needReturnConversion = false;
                                if (signature.RetType is TypeSpecVoidTag)
                                {
                                    if (returnDest != null)
                                        throw new RpaCompileException("Return type is not void");
                                }
                                else if (returnDest != null)
                                {
                                    needReturnConversion = !this.Compiler.TypeIsValueType(signature.RetType);
                                    if (needReturnConversion)
                                        tempReturn = new HighSsaRegister(HighValueType.ReferenceValue, signature.RetType, null);
                                }

                                MethodSpecTag generatedMethodSpec = new MethodSpecTag(MethodSlotType.Instance, methodSpec.GenericParameters, cls.TypeSpec, resolvedMethod.MethodDeclTag);
                                generatedMethodSpec = this.Compiler.TagRepository.InternMethodSpec(generatedMethodSpec);

                                MethodHandle methodHandle = this.Compiler.InstantiateMethod(generatedMethodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                                CheckMethodCall(generatedMethodSpec, tempReturn, tempParameters, resolvedMethod.MethodSignature);

                                newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, tempReturn, refInstance, tempParameters));

                                if (needReturnConversion)
                                    EmitPassiveRefConversion(tInstr.CodeLocation, returnDest, tempReturn, newInstrs);
                            }
                            else
                            {
                                CliClass resolvedClass = cls;
                                while (depth > 0)
                                {
                                    resolvedClass = this.Compiler.GetClosedClass(resolvedClass.ParentClassSpec);
                                    depth--;
                                }

                                resolvedMethod = resolvedClass.Methods[methodIndex.Index];

                                // Method is on the parent of a value type, so it must be boxed
                                HighSsaRegister boxed = new HighSsaRegister(HighValueType.BoxedValue, instanceType, null);
                                newInstrs.Add(new Clarity.Rpa.Instructions.BoxInstruction(tInstr.CodeLocation, boxed, refInstance));

                                HighSsaRegister newInstanceSrc = new HighSsaRegister(HighValueType.ReferenceValue, resolvedClass.TypeSpec, null);
                                newInstrs.Add(new Instructions.ObjectToObjectInstruction(tInstr.CodeLocation, newInstanceSrc, boxed));

                                MethodSpecTag generatedMethodSpec = new MethodSpecTag(MethodSlotType.Instance, methodSpec.GenericParameters, resolvedClass.TypeSpec, resolvedMethod.MethodDeclTag);
                                generatedMethodSpec = this.Compiler.TagRepository.InternMethodSpec(generatedMethodSpec);

                                MethodHandle methodHandle = this.Compiler.InstantiateMethod(generatedMethodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                                CheckMethodCall(generatedMethodSpec, dest, parameters, resolvedMethod.MethodSignature);

                                newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, dest, newInstanceSrc, parameters));
                            }
                        }
                        else
                        {
                            HighSsaRegister loadedInstance = new HighSsaRegister(HighValueType.ReferenceValue, refInstance.Type, null);
                            newInstrs.Add(new LoadPtrInstruction(tInstr.CodeLocation, loadedInstance, refInstance));

                            if (methodSpec.GenericParameters.Length == 0)
                            {
                                HighSsaRegister instance = new HighSsaRegister(HighValueType.ReferenceValue, constraintType, null);
                                EmitPassiveRefConversion(tInstr.CodeLocation, instance, loadedInstance, newInstrs);

                                if (isInterface)
                                {
                                    CliInterface ifc = this.Compiler.GetClosedInterface((TypeSpecClassTag)constraintType);
                                    uint vtableSlotIndex = ifc.CliSlotForSlotTag(methodSpec.MethodDecl);
                                    MethodSignatureTag methodSignature = ifc.Slots[vtableSlotIndex].Signature;

                                    CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                                    newInstrs.Add(new Instructions.CallRloInterfaceMethodInstruction(tInstr.CodeLocation, vtableSlotIndex, dest, instance, parameters));
                                }
                                else
                                {
                                    if (constraintType is TypeSpecClassTag || constraintType is TypeSpecArrayTag)
                                    {
                                        TypeSpecClassTag methodInstanceClass = methodSpec.DeclaringClass;
                                        if (this.Compiler.TypeIsValueType(methodInstanceClass))
                                            throw new RpaCompileException("CallConstrainedVirtualMethod method spec is from a value type");

                                        HighSsaRegister convertedInstance = new HighSsaRegister(HighValueType.ReferenceValue, methodInstanceClass, null);
                                        EmitPassiveRefConversion(tInstr.CodeLocation, convertedInstance, loadedInstance, newInstrs);

                                        if (this.Compiler.TypeIsInterface(methodInstanceClass))
                                        {
                                            CliInterface ifcClass = this.Compiler.GetClosedInterface(methodInstanceClass);
                                            uint vtableSlotIndex;
                                            vtableSlotIndex = ifcClass.CliSlotForSlotTag(methodSpec.MethodDecl);
                                            MethodSignatureTag methodSignature = ifcClass.Slots[vtableSlotIndex].Signature;

                                            CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                                            newInstrs.Add(new Instructions.CallRloInterfaceMethodInstruction(tInstr.CodeLocation, vtableSlotIndex, dest, convertedInstance, parameters));
                                        }
                                        else
                                        {
                                            CliClass cliClass = this.Compiler.GetClosedClass(methodInstanceClass);
                                            uint vtableSlotIndex;
                                            if (!cliClass.DeclTagToVTableSlot.TryGetValue(methodSpec.MethodDecl, out vtableSlotIndex))
                                                throw new RpaCompileException("CallConstrainedVirtualMethod method wasn't found");
                                            MethodSignatureTag methodSignature = cliClass.VTable[vtableSlotIndex].MethodSignature;

                                            CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                                            newInstrs.Add(new Instructions.CallRloVirtualMethodInstruction(tInstr.CodeLocation, vtableSlotIndex, dest, convertedInstance, parameters));
                                        }
                                    }
                                    else
                                        throw new RpaCompileException("Unexpected constraint type");
                                }
                            }
                            else
                            {
                                // Constrained generic call on a reference value
                                TypeSpecClassTag declaringClass = methodSpec.DeclaringClass;
                                HighTypeDef typeDef = this.Compiler.GetTypeDef(declaringClass.TypeName);

                                TypeSpecClassTag instanceClassTag = instanceType as TypeSpecClassTag;
                                if (instanceClassTag == null)
                                    throw new RpaCompileException("Constrained generic call site a non-class");

                                HighTypeDef instanceTypeDef = this.Compiler.GetTypeDef(instanceClassTag.TypeName);
                                if (instanceTypeDef.Semantics != TypeSemantics.Class)
                                {
                                    MethodInstantiationPath path = GenerateMethodInstantiationPath(tInstr.CodeLocation);
                                    throw new RpaCompileException("Constrained generic call on a non-class: " + path.ToString());
                                }

                                if (!instanceTypeDef.IsSealed)
                                {
                                    MethodInstantiationPath path = GenerateMethodInstantiationPath(tInstr.CodeLocation);
                                    throw new RpaCompileException("Constrained generic call on a class that isn't sealed: " + path.ToString());
                                }

                                CliClass instanceClass = this.Compiler.GetClosedClass(instanceClassTag);
                                uint vtableSlotIndex;

                                switch (typeDef.Semantics)
                                {
                                    case TypeSemantics.Interface:
                                        {
                                            CliInterface ifc = this.Compiler.GetClosedInterface(declaringClass);
                                            uint ifcSlot = ifc.CliSlotForSlotTag(methodSpec.MethodDecl);

                                            vtableSlotIndex = this.Compiler.DevirtualizeInterfaceMethod(instanceClass, declaringClass, ifcSlot);
                                        }
                                        break;
                                    case TypeSemantics.Class:
                                        {
                                            CliClass cls = this.Compiler.GetClosedClass(declaringClass);
                                            if (!cls.DeclTagToVTableSlot.TryGetValue(methodSpec.MethodDecl, out vtableSlotIndex))
                                                throw new RpaCompileException("CallConstrainedVirtualMethod vtable slot had no match");
                                        }
                                        break;
                                    default:
                                        throw new RpaCompileException("Unexpected semantics on generic call on reference type");
                                }

                                CliVtableSlot vtableSlot = instanceClass.VTable[vtableSlotIndex];
                                CliMethodIndex methodIndex = vtableSlot.MethodIndex;

                                uint depth = methodIndex.Depth;
                                CliClass instanceConversionTargetClass = instanceClass;
                                while (depth > 0)
                                {
                                    instanceConversionTargetClass = instanceConversionTargetClass.ParentClass;
                                    depth--;
                                }

                                HighMethod resolvedMethod = instanceConversionTargetClass.Methods[methodIndex.Index];

                                if (resolvedMethod.MethodSignature.NumGenericParameters != methodSpec.GenericParameters.Length)
                                    throw new RpaCompileException("Constrained generic call parameter type mismatch");

                                // Convert instance
                                HighSsaRegister convertedInstance = new HighSsaRegister(HighValueType.ReferenceValue, instanceConversionTargetClass.TypeSpec, null);
                                EmitPassiveRefConversion(tInstr.CodeLocation, convertedInstance, loadedInstance, newInstrs);

                                // Convert parameters
                                MethodSignatureTag methodSignature = resolvedMethod.MethodSignature.Instantiate(this.Compiler.TagRepository, new TypeSpecTag[0], methodSpec.GenericParameters);

                                int numParameters = methodSignature.ParamTypes.Length;
                                if (numParameters != parameters.Length)
                                    throw new RpaCompileException("Constrained generic call parameter count mismatch");

                                List<HighSsaRegister> convertedParameters = new List<HighSsaRegister>();
                                for (int i = 0; i < numParameters; i++)
                                {
                                    MethodSignatureParam param = methodSignature.ParamTypes[i];
                                    switch (param.TypeOfType.Value)
                                    {
                                        case MethodSignatureParamTypeOfType.Values.ByRef:
                                            convertedParameters.Add(parameters[i]);
                                            break;
                                        case MethodSignatureParamTypeOfType.Values.Value:
                                            {
                                                if (this.Compiler.TypeIsValueType(param.Type))
                                                    convertedParameters.Add(parameters[i]);
                                                else
                                                {
                                                    HighSsaRegister convertedParameter = new HighSsaRegister(HighValueType.ReferenceValue, param.Type, null);
                                                    EmitPassiveRefConversion(tInstr.CodeLocation, convertedParameter, parameters[i], newInstrs);
                                                    convertedParameters.Add(convertedParameter);
                                                }
                                            }
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }

                                parameters = convertedParameters.ToArray();

                                HighSsaRegister unconvertedReturnDest = tInstr.ReturnDest;
                                HighSsaRegister convertedReturnDest = null;
                                if (unconvertedReturnDest != null)
                                {
                                    if (this.Compiler.TypeIsValueType(methodSignature.RetType))
                                        convertedReturnDest = unconvertedReturnDest;
                                    else
                                    {
                                        unconvertedReturnDest = new HighSsaRegister(HighValueType.ReferenceValue, methodSignature.RetType, null);
                                        convertedReturnDest = tInstr.ReturnDest;
                                    }
                                }

                                MethodSpecTag generatedMethodSpec = new MethodSpecTag(MethodSlotType.Instance, methodSpec.GenericParameters, instanceConversionTargetClass.TypeSpec, resolvedMethod.MethodDeclTag);
                                generatedMethodSpec = this.Compiler.TagRepository.InternMethodSpec(generatedMethodSpec);

                                MethodHandle methodHandle = this.Compiler.InstantiateMethod(generatedMethodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                                CheckMethodCall(generatedMethodSpec, unconvertedReturnDest, parameters, resolvedMethod.MethodSignature);

                                newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, unconvertedReturnDest, convertedInstance, parameters));

                                if (unconvertedReturnDest != convertedReturnDest)
                                    EmitPassiveRefConversion(tInstr.CodeLocation, convertedReturnDest, unconvertedReturnDest, newInstrs);
                            }
                        }
                    }
                    break;

                case HighInstruction.Opcodes.CompareNumbers:
                    {
                        CompareNumbersInstruction tInstr = (CompareNumbersInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest == null)
                            throw new RpaCompileException("CompareNumbers has no destination");

                        if (dest.ValueType != HighValueType.ValueValue || dest.Type != m_boolType)
                            throw new RpaCompileException("CompareNumbers has an invalid destination type");

                        TypeSpecClassTag expectedType = ExpectedClassForArithType(tInstr.NumberType);

                        switch (tInstr.Left.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                break;
                            default:
                                throw new RpaCompileException("CompareNumbers has an invalid operand");
                        }

                        switch (tInstr.Right.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                break;
                            default:
                                throw new RpaCompileException("CompareNumbers has an invalid operand");
                        }

                        if (tInstr.Left.Type != expectedType || tInstr.Right.Type != expectedType)
                            throw new RpaCompileException("CompareNumbers operands are the wrong type");
                    }
                    break;
                case HighInstruction.Opcodes.GetArrayLength:
                    {
                        GetArrayLengthInstruction tInstr = (GetArrayLengthInstruction)instr;

                        HighSsaRegister dest = tInstr.Dest;
                        HighSsaRegister src = tInstr.Src;
                        if (dest != null)
                        {
                            if (dest.ValueType != HighValueType.ValueValue || dest.Type != m_nativeUIntType)
                                throw new RpaCompileException("GetArrayLength invalid destination type");
                        }

                        switch (src.ValueType)
                        {
                            case HighValueType.Null:
                                break;
                            case HighValueType.ReferenceValue:
                                {
                                    TypeSpecArrayTag srcType = src.Type as TypeSpecArrayTag;
                                    if (srcType == null)
                                        throw new RpaCompileException("GetArrayLength operand isn't an array");
                                    if (srcType.Rank != 1)
                                        throw new RpaCompileException("GetArrayLength operand isn't 1-rank");
                                }
                                break;
                            default:
                                throw new RpaCompileException("GetArrayLength operand isn't a reference");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.ReturnValue:
                    {
                        ReturnValueInstruction tInstr = (ReturnValueInstruction)instr;

                        HighSsaRegister value = tInstr.Value;

                        TypeSpecTag retType = this.MethodBody.ReturnType;
                        if (retType is TypeSpecVoidTag)
                            throw new RpaCompileException("ReturnValue in a function that has no return type");

                        bool isValueType = this.Compiler.TypeIsValueType(retType);

                        bool expectValueType;
                        switch (tInstr.Value.ValueType)
                        {
                            case HighValueType.Null:
                            case HighValueType.ConstantString:
                            case HighValueType.ReferenceValue:
                                expectValueType = false;
                                break;
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                expectValueType = true;
                                break;
                            default:
                                throw new RpaCompileException("ReturnValue invalid return value type");
                        }

                        if (expectValueType != isValueType)
                            throw new RpaCompileException("Incompatible return value type");

                        if (tInstr.Value.ValueType != HighValueType.Null && tInstr.Value.Type != retType)
                            throw new RpaCompileException("Incompatible return value type");
                    }
                    break;
                case HighInstruction.Opcodes.GetLocalPtr:
                    {
                        GetLocalPtrInstruction tInstr = (GetLocalPtrInstruction)instr;

                        HighLocal local = tInstr.Local;
                        if (local.TypeOfType != HighLocal.ETypeOfType.Value)
                            throw new RpaCompileException("GetLocalPtr local isn't a value");

                        HighSsaRegister dest = tInstr.Dest;
                        if (dest == null)
                            throw new RpaCompileException("GetLocalPtr has no destination");

                        if (dest.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("GetLocalPtr destination isn't a managed pointer");

                        if (dest.Type != local.Type)
                            throw new RpaCompileException("GetLocalPtr destination isn't the same type as the local");
                    }
                    break;
                case HighInstruction.Opcodes.UnaryArith:
                    {
                        UnaryArithInstruction tInstr = (UnaryArithInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("UnaryArith has no destination");

                        TypeSpecClassTag expectedClass = ExpectedClassForArithType(tInstr.ArithType);

                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                break;
                            default:
                                throw new RpaCompileException("UnaryArith source type is invalid");
                        }

                        if (tInstr.Dest.ValueType != HighValueType.ValueValue)
                            throw new RpaCompileException("UnaryArith destination type is invalid");

                        if (tInstr.Src.Type != expectedClass || tInstr.Dest.Type != expectedClass)
                            throw new RpaCompileException("UnaryArith type doesn't match");
                    }
                    break;
                case HighInstruction.Opcodes.StoreLocal:
                    {
                        StoreLocalInstruction tInstr = (StoreLocalInstruction)instr;

                        HighLocal local = tInstr.Local;
                        HighSsaRegister src = tInstr.Src;
                        switch (local.TypeOfType)
                        {
                            case HighLocal.ETypeOfType.ByRef:
                                if (src.ValueType != HighValueType.ManagedPtr)
                                    throw new RpaCompileException("StoreLocal type mismatch");
                                if (src.Type != local.Type)
                                    throw new RpaCompileException("StoreLocal type mismatch");
                                break;
                            case HighLocal.ETypeOfType.Value:
                                {
                                    bool isValueType = this.Compiler.TypeIsValueType(local.Type);

                                    if (isValueType)
                                    {
                                        switch (src.ValueType)
                                        {
                                            case HighValueType.ConstantValue:
                                            case HighValueType.ValueValue:
                                                if (src.Type != local.Type)
                                                    throw new RpaCompileException("StoreLocal type mismatch");
                                                break;
                                            default:
                                                throw new RpaCompileException("StoreLocal source type is invalid");
                                        }
                                    }
                                    else
                                    {
                                        switch (src.ValueType)
                                        {
                                            case HighValueType.ConstantString:
                                            case HighValueType.ReferenceValue:
                                                if (src.Type != local.Type)
                                                    throw new RpaCompileException("StoreLocal type mismatch");
                                                break;
                                            case HighValueType.Null:
                                                break;
                                            default:
                                                throw new RpaCompileException("StoreLocal source type is invalid");
                                        }
                                    }
                                }
                                break;
                            case HighLocal.ETypeOfType.TypedByRef:
                                throw new NotImplementedException();
                            default:
                                throw new ArgumentException();
                        }
                    }
                    break;
                case HighInstruction.Opcodes.UnboxPtr:
                    {
                        UnboxPtrInstruction tInstr = (UnboxPtrInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("UnboxPtr has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("UnboxPtr destination isn't a managed pointer");

                        if (!this.Compiler.TypeIsValueType(tInstr.Dest.Type))
                            throw new RpaCompileException("UnboxPtr type isn't a value type");

                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:  // Grumble grumble
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("UnboxPtr source isn't a reference type");
                        }

                        if (tInstr.Src.Type != m_objectType)
                            throw new RpaCompileException("UnboxPtr source isn't System.Object");
                    }
                    break;
                case HighInstruction.Opcodes.ZeroFillPtr:
                    {
                        ZeroFillPtrInstruction tInstr = (ZeroFillPtrInstruction)instr;
                        if (tInstr.Target.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("ZeroFillPtr target isn't a managed pointer");
                    }
                    break;
                case HighInstruction.Opcodes.UnboxValue:
                    {
                        UnboxValueInstruction tInstr = (UnboxValueInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("UnboxValue has no destination");

                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:  // Grumble grumble
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("UnboxValue source isn't a reference type");
                        }

                        if (tInstr.Src.Type != m_objectType)
                            throw new RpaCompileException("UnboxValue source isn't System.Object");
                        
                        validationOnly = true;

                        switch (tInstr.Dest.ValueType)
                        {
                            case HighValueType.ValueValue:
                                {
                                    HighSsaRegister ptr = new HighSsaRegister(HighValueType.ManagedPtr, tInstr.Dest.Type, null);
                                    newInstrs.Add(new UnboxPtrInstruction(tInstr.CodeLocation, ptr, tInstr.Src));
                                    newInstrs.Add(new LoadPtrInstruction(tInstr.CodeLocation, tInstr.Dest, ptr));
                                }
                                break;
                            case HighValueType.ReferenceValue:
                                newInstrs.Add(new ForceDynamicCastInstruction(tInstr.CodeLocation, tInstr.Dest, tInstr.Src));
                                break;
                            default:
                                throw new RpaCompileException("UnboxValue destination is invalid");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.Switch:
                    {
                        SwitchInstruction tInstr = (SwitchInstruction)instr;

                        switch (tInstr.Value.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                break;
                            default:
                                throw new RpaCompileException("Switch source is invalid");
                        }

                        if (tInstr.Value.Type != m_uint32Type)
                            throw new RpaCompileException("Switch source isn't a UInt32");
                    }
                    break;
                case HighInstruction.Opcodes.Throw:
                    {
                        ThrowInstruction tInstr = (ThrowInstruction)instr;

                        switch (tInstr.Exception.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("Throw instruction doesn't throw an object");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.StorePtr:
                    {
                        StorePtrInstruction tInstr = (StorePtrInstruction)instr;

                        if (tInstr.Ptr.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("StorePtr destination isn't a managed pointer");

                        bool isValueType = this.Compiler.TypeIsValueType(tInstr.Ptr.Type);

                        switch (tInstr.Value.ValueType)
                        {
                            case HighValueType.ConstantString:
                            case HighValueType.ConstantValue:
                            case HighValueType.ReferenceValue:
                            case HighValueType.ValueValue:
                                if (tInstr.Value.Type != tInstr.Ptr.Type)
                                    throw new RpaCompileException("StorePtr type mismatch");
                                break;
                            case HighValueType.Null:
                                if (isValueType)
                                    throw new RpaCompileException("StorePtr type mismatch");
                                break;
                            default:
                                throw new RpaCompileException("StorePtr source is invalid");
                        }
                    }
                    break;
                case HighInstruction.Opcodes.GetFieldInfo:
                    {
                        GetFieldInfoInstruction tInstr = (GetFieldInfoInstruction)instr;

                        TypeSpecClassTag typeClassSpec = tInstr.Type as TypeSpecClassTag;
                        if (typeClassSpec == null)
                            throw new RpaCompileException("GetFieldInfo type isn't a class");

                        if (!this.Compiler.HaveCliOpenClass(typeClassSpec.TypeName))
                            throw new RpaCompileException("GetFieldInfo type name isn't a class");

                        CliClass cls = this.Compiler.GetClosedClass(typeClassSpec);

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("GetFieldInfo has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ValueValue && tInstr.Type != m_runtimeFieldHandleType)
                            throw new RpaCompileException("GetFieldInfo destination is invalid");

                        IDictionary<string, uint> fieldDict = tInstr.IsStatic ? cls.NameToStaticFieldSlot : cls.NameToInstanceFieldSlot;

                        uint fieldIndex;
                        if (!fieldDict.TryGetValue(tInstr.FieldName, out fieldIndex))
                            throw new RpaCompileException("GetFieldInfo field not found");

                        validationOnly = false;
                        newInstrs.Add(new Instructions.GetRloFieldInfoInstruction(tInstr.CodeLocation, tInstr.Dest, tInstr.Type, fieldIndex, tInstr.IsStatic));
                    }
                    break;
                case HighInstruction.Opcodes.LoadValueField:
                    {
                        LoadValueFieldInstruction tInstr = (LoadValueFieldInstruction)instr;
                        validationOnly = false;

                        if (tInstr.Src.ValueType != HighValueType.ValueValue)
                            throw new RpaCompileException("LoadValueField source is invalid");
                        
                        TypeSpecClassTag typeClassSpec = tInstr.Src.Type as TypeSpecClassTag;
                        if (typeClassSpec == null)
                            throw new RpaCompileException("LoadValueField type isn't a class");

                        if (!this.Compiler.HaveCliOpenClass(typeClassSpec.TypeName))
                            throw new RpaCompileException("LoadValueField type name isn't a class");

                        CliClass cls = this.Compiler.GetClosedClass(typeClassSpec);

                        uint fieldIndex;
                        if (!cls.NameToInstanceFieldSlot.TryGetValue(tInstr.FieldName, out fieldIndex))
                            throw new RpaCompileException("LoadValueField field not found");

                        HighField fld = cls.InstanceFields[fieldIndex];

                        switch (tInstr.Dest.ValueType)
                        {
                            case HighValueType.ValueValue:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("LoadValueField destination isn't a value");
                        }

                        if (tInstr.Dest.Type != fld.Type)
                            throw new RpaCompileException("LoadValueField destination type mismatch");

                        validationOnly = false;
                        newInstrs.Add(new Instructions.LoadValueRloFieldInstruction(tInstr.CodeLocation, tInstr.Dest, tInstr.Src, fieldIndex));
                    }
                    break;
                case HighInstruction.Opcodes.BindStaticDelegate:
                    {
                        BindStaticDelegateInstruction tInstr = (BindStaticDelegateInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("BindStaticDelegate has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("BindStaticDelegate target isn't a reference");

                        TypeSpecClassTag destClass = tInstr.Dest.Type as TypeSpecClassTag;
                        if (destClass == null)
                            throw new RpaCompileException("BindStaticDelegate destination isn't a class");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(destClass.TypeName);
                        if (typeDef.Semantics != TypeSemantics.Delegate)
                            throw new RpaCompileException("BindStaticDelegate destination isn't a delegate");

                        if (tInstr.MethodSpec.MethodSlotType != MethodSlotType.Static)
                            throw new RpaCompileException("BindStaticDelegate method spec isn't static");

                        TypeSpecDelegateTag dgTag = new TypeSpecDelegateTag(destClass, tInstr.MethodSpec);
                        dgTag = (TypeSpecDelegateTag)this.Compiler.TagRepository.InternTypeSpec(dgTag);

                        validationOnly = true;
                        HighSsaRegister sdInstance = new HighSsaRegister(HighValueType.ReferenceValue, dgTag, null);

                        newInstrs.Add(new AllocObjInstruction(tInstr.CodeLocation, sdInstance, dgTag));
                        newInstrs.Add(new Instructions.ObjectToObjectInstruction(tInstr.CodeLocation, tInstr.Dest, sdInstance));
                    }
                    break;
                case HighInstruction.Opcodes.BindInstanceDelegate:
                    {
                        BindInstanceDelegateInstruction tInstr = (BindInstanceDelegateInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("BindInstanceDelegate has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("BindInstanceDelegate target isn't a reference");

                        TypeSpecClassTag destClass = tInstr.Dest.Type as TypeSpecClassTag;
                        if (destClass == null)
                            throw new RpaCompileException("BindInstanceDelegate destination isn't a class");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(destClass.TypeName);
                        if (typeDef.Semantics != TypeSemantics.Delegate)
                            throw new RpaCompileException("BindInstanceDelegate destination isn't a delegate");

                        if (tInstr.MethodSpec.MethodSlotType != MethodSlotType.Instance)
                            throw new RpaCompileException("BindInstanceDelegate method spec isn't an instance method");

                        switch (tInstr.Object.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("BindInstanceDelegate object is invalid");
                        }

                        if (tInstr.MethodSpec.DeclaringClass != tInstr.Object.Type)
                            throw new RpaCompileException("BindInstanceDelegate method spec type doesn't match source");

                        if (tInstr.MethodSpec.MethodSlotType != MethodSlotType.Instance)
                            throw new RpaCompileException("BindInstanceDelegate method spec isn't an instance method");

                        TypeSpecDelegateTag dgTag = new TypeSpecDelegateTag(destClass, tInstr.MethodSpec);
                        dgTag = (TypeSpecDelegateTag)this.Compiler.TagRepository.InternTypeSpec(dgTag);

                        validationOnly = true;
                        HighSsaRegister dgInstance = new HighSsaRegister(HighValueType.ReferenceValue, dgTag, null);

                        newInstrs.Add(new Instructions.AllocInstanceDelegateInstruction(tInstr.CodeLocation, dgTag, dgInstance, tInstr.Object));
                        newInstrs.Add(new Instructions.ObjectToObjectInstruction(tInstr.CodeLocation, tInstr.Dest, dgInstance));
                    }
                    break;
                case HighInstruction.Opcodes.BindVirtualDelegate:
                    {
                        BindVirtualDelegateInstruction tInstr = (BindVirtualDelegateInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("BindVirtualDelegate has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("BindVirtualDelegate target isn't a reference");

                        TypeSpecClassTag destClass = tInstr.Dest.Type as TypeSpecClassTag;
                        if (destClass == null)
                            throw new RpaCompileException("BindVirtualDelegate destination isn't a class");

                        HighTypeDef typeDef = this.Compiler.GetTypeDef(destClass.TypeName);
                        if (typeDef.Semantics != TypeSemantics.Delegate)
                            throw new RpaCompileException("BindVirtualDelegate destination isn't a delegate");

                        if (tInstr.MethodSpec.MethodSlotType != MethodSlotType.Virtual)
                            throw new RpaCompileException("BindVirtualDelegate method spec isn't an instance method");

                        switch (tInstr.Object.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                break;
                            default:
                                throw new RpaCompileException("BindInstanceDelegate object is invalid");
                        }

                        if (tInstr.MethodSpec.DeclaringClass != tInstr.Object.Type)
                            throw new RpaCompileException("BindInstanceDelegate method spec type doesn't match source");

                        if (tInstr.MethodSpec.MethodSlotType != MethodSlotType.Virtual)
                            throw new RpaCompileException("BindInstanceDelegate method spec isn't a virtual method");

                        TypeSpecDelegateTag dgTag = new TypeSpecDelegateTag(destClass, tInstr.MethodSpec);
                        dgTag = (TypeSpecDelegateTag)this.Compiler.TagRepository.InternTypeSpec(dgTag);

                        validationOnly = true;
                        HighSsaRegister dgInstance = new HighSsaRegister(HighValueType.ReferenceValue, dgTag, null);

                        newInstrs.Add(new Instructions.AllocInstanceDelegateInstruction(tInstr.CodeLocation, dgTag, dgInstance, tInstr.Object));
                        newInstrs.Add(new Instructions.ObjectToObjectInstruction(tInstr.CodeLocation, tInstr.Dest, dgInstance));
                    }
                    break;
                case HighInstruction.Opcodes.Catch:
                    {
                        CatchInstruction tInstr = (CatchInstruction)instr;
                        if (tInstr.Dest == null)
                            throw new RpaCompileException("Catch instruction has no destination");

                        if (tInstr.Dest.ValueType != HighValueType.ReferenceValue)
                            throw new RpaCompileException("Catch instruction destination is invalid");
                    }
                    break;
                case HighInstruction.Opcodes.NumberConvert:
                    {
                        validationOnly = false;

                        NumberConvertInstruction tInstr = (NumberConvertInstruction)instr;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("NumberConvert instruction destination is invalid");
                        if (tInstr.Dest.ValueType != HighValueType.ValueValue)
                            break;
                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                break;
                            default:
                                throw new RpaCompileException("NumberConvert source is invalid");
                        }

                        EmitNumberConversion(tInstr.CodeLocation, tInstr.Dest, tInstr.Src, tInstr.CheckOverflow, newInstrs);
                    }
                    break;
                case HighInstruction.Opcodes.PassiveConvert:
                    {
                        PassiveConvertInstruction tInstr = (PassiveConvertInstruction)instr;

                        validationOnly = false;

                        if (tInstr.Dest == null)
                            throw new RpaCompileException("PassiveConvert has no destination");

                        bool srcIsValue;
                        switch (tInstr.Src.ValueType)
                        {
                            case HighValueType.ConstantValue:
                            case HighValueType.ValueValue:
                                srcIsValue = true;
                                break;
                            case HighValueType.BoxedValue:
                            case HighValueType.ConstantString:
                            case HighValueType.Null:
                            case HighValueType.ReferenceValue:
                                srcIsValue = false;
                                break;
                            default:
                                throw new RpaCompileException("PassiveConvert invalid source type");
                        }

                        bool destIsValue;
                        switch (tInstr.Dest.ValueType)
                        {
                            case HighValueType.BoxedValue:
                            case HighValueType.ReferenceValue:
                                destIsValue = false;
                                break;
                            case HighValueType.ValueValue:
                                destIsValue = true;
                                break;
                            default:
                                throw new RpaCompileException("PassiveConvert invalid dest type");
                        }

                        if (destIsValue != srcIsValue)
                            throw new RpaCompileException("PassiveConvert ref/value mismatch");

                        if (srcIsValue)
                            EmitPassiveValueConversion(tInstr.CodeLocation, tInstr.Dest, tInstr.Src, newInstrs);
                        else
                            EmitPassiveRefConversion(tInstr.CodeLocation, tInstr.Dest, tInstr.Src, newInstrs);
                    }
                    break;
                case HighInstruction.Opcodes.CallConstrainedMethod:
                    {
                        validationOnly = false;

                        CallConstrainedMethodInstruction tInstr = (CallConstrainedMethodInstruction)instr;
                        HighSsaRegister refInstance = tInstr.InstanceSrc;
                        HighSsaRegister[] parameters = tInstr.Parameters;
                        HighSsaRegister dest = tInstr.ReturnDest;
                        MethodSpecTag methodSpec = tInstr.MethodSpec;

                        if (methodSpec.MethodSlotType != MethodSlotType.Instance)
                            throw new RpaCompileException("CallConstrainedMethodInstruction target is not an instance method");

                        TypeSpecTag constraintType = tInstr.ConstraintType;

                        if (refInstance.ValueType != HighValueType.ManagedPtr)
                            throw new RpaCompileException("CallConstrainedMethodInstruction target is not a managed pointer");

                        if (refInstance.Type != constraintType)
                            throw new RpaCompileException("CallConstrainedMethodInstruction target type is different from constraint type");

                        TypeSpecTag instanceType = refInstance.Type;

                        bool isValueType;
                        bool isInterface;

                        switch (instanceType.SubType)
                        {
                            case TypeSpecTag.SubTypeCode.Array:
                                isValueType = false;
                                isInterface = false;
                                break;
                            case TypeSpecTag.SubTypeCode.Class:
                                {
                                    TypeSpecClassTag instanceClassTag = (TypeSpecClassTag)instanceType;
                                    HighTypeDef instanceTypeDef = this.Compiler.GetTypeDef(instanceClassTag.TypeName);

                                    switch (instanceTypeDef.Semantics)
                                    {
                                        case TypeSemantics.Class:
                                        case TypeSemantics.Delegate:
                                            isValueType = false;
                                            isInterface = false;
                                            break;
                                        case TypeSemantics.Interface:
                                            isValueType = false;
                                            isInterface = true;
                                            break;
                                        case TypeSemantics.Enum:
                                        case TypeSemantics.Struct:
                                            isValueType = true;
                                            isInterface = false;
                                            break;
                                        default:
                                            throw new NotSupportedException();
                                    }
                                }
                                break;
                            default:
                                throw new RpaCompileException("Invalid instance type in CallConstrainedVirtualMethod");
                        };

                        if (isValueType)
                        {
                            TypeSpecClassTag instanceClassTag = (TypeSpecClassTag)refInstance.Type;
                            CliClass cls = this.Compiler.GetClosedClass(instanceClassTag);

                            TypeSpecClassTag methodDeclaringClass = methodSpec.DeclaringClass;

                            HighTypeDef methodTypeDef = this.Compiler.GetTypeDef(methodDeclaringClass.TypeName);

                            HighMethod resolvedMethod;
                            uint methodIndex = 0;
                            if (methodTypeDef.Semantics != TypeSemantics.Class)
                                throw new RpaCompileException("CallConstrainedMethod declaring type isn't a class");

                            CliClass resolvedClass = cls;
                            while (resolvedClass != null)
                            {
                                if (resolvedClass.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodIndex))
                                    break;
                                resolvedClass = resolvedClass.ParentClass;
                            }

                            if (resolvedClass == null)
                                throw new RpaCompileException("CallConstrainedMethod virtual method was not found");

                            resolvedMethod = resolvedClass.Methods[methodIndex];

                            HighSsaRegister boxed = new HighSsaRegister(HighValueType.BoxedValue, instanceType, null);
                            newInstrs.Add(new BoxInstruction(tInstr.CodeLocation, boxed, refInstance));

                            HighSsaRegister newInstanceSrc = new HighSsaRegister(HighValueType.ReferenceValue, resolvedClass.TypeSpec, null);
                            newInstrs.Add(new Instructions.ObjectToObjectInstruction(tInstr.CodeLocation, newInstanceSrc, boxed));

                            MethodSpecTag generatedMethodSpec = new MethodSpecTag(MethodSlotType.Instance, methodSpec.GenericParameters, resolvedClass.TypeSpec, resolvedMethod.MethodDeclTag);
                            generatedMethodSpec = this.Compiler.TagRepository.InternMethodSpec(generatedMethodSpec);

                            MethodHandle methodHandle = this.Compiler.InstantiateMethod(generatedMethodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));

                            CheckMethodCall(generatedMethodSpec, dest, parameters, resolvedMethod.MethodSignature);

                            newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, dest, newInstanceSrc, parameters));
                        }
                        else
                        {
                            HighSsaRegister loadedInstance = new HighSsaRegister(HighValueType.ReferenceValue, refInstance.Type, null);
                            newInstrs.Add(new LoadPtrInstruction(tInstr.CodeLocation, loadedInstance, refInstance));

                            if (methodSpec.GenericParameters.Length != 0)
                                throw new RpaCompileException("Generic method spec on a non-virtual method (???)");

                            HighSsaRegister instance = new HighSsaRegister(HighValueType.ReferenceValue, constraintType, null);
                            EmitPassiveRefConversion(tInstr.CodeLocation, instance, loadedInstance, newInstrs);

                            if (isInterface)
                            {
                                HighSsaRegister objReg = new HighSsaRegister(HighValueType.ReferenceValue, m_objectType, null);
                                newInstrs.Add(new Instructions.InterfaceToObjectInstruction(tInstr.CodeLocation, objReg, loadedInstance));

                                if (methodSpec.DeclaringClass != m_objectType)
                                    throw new RpaCompileException("Constrained method on an interface isn't System.Object");

                                uint methodIndex;
                                CliClass objClass = this.Compiler.GetClosedClass(m_objectType);
                                if (!objClass.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodIndex))
                                    throw new RpaCompileException("Constrained method on System.Object not found");

                                HighMethod resolvedMethod = objClass.Methods[methodIndex];
                                MethodSignatureTag methodSignature = resolvedMethod.MethodSignature;

                                CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                                MethodHandle methodHandle = this.Compiler.InstantiateMethod(methodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));
                                newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, dest, instance, parameters));
                            }
                            else
                            {
                                if (constraintType is TypeSpecClassTag || constraintType is TypeSpecArrayTag)
                                {
                                    TypeSpecClassTag methodInstanceClass = methodSpec.DeclaringClass;
                                    if (this.Compiler.TypeIsValueType(methodInstanceClass))
                                        throw new RpaCompileException("CallConstrainedMethod method spec is from a value type");

                                    HighSsaRegister convertedInstance = new HighSsaRegister(HighValueType.ReferenceValue, methodInstanceClass, null);
                                    EmitPassiveRefConversion(tInstr.CodeLocation, convertedInstance, loadedInstance, newInstrs);

                                    if (this.Compiler.TypeIsInterface(methodInstanceClass))
                                        throw new RpaCompileException("CallConstrainedMethod target class was an interface");
                                    else
                                    {
                                        CliClass cliClass = this.Compiler.GetClosedClass(methodInstanceClass);
                                        uint methodIndex;

                                        if (!cliClass.DeclTagToMethod.TryGetValue(methodSpec.MethodDecl, out methodIndex))
                                            throw new RpaCompileException("CallConstrainedMethod method wasn't found");

                                        HighMethod resolvedMethod = cliClass.Methods[methodIndex];
                                        MethodSignatureTag methodSignature = resolvedMethod.MethodSignature;

                                        MethodHandle methodHandle = this.Compiler.InstantiateMethod(methodSpec, GenerateMethodInstantiationPath(tInstr.CodeLocation));
                                        CheckMethodCall(methodSpec, dest, parameters, methodSignature);

                                        newInstrs.Add(new Instructions.CallRloInstanceMethodInstruction(tInstr.CodeLocation, methodHandle, dest, convertedInstance, parameters));
                                    }
                                }
                                else
                                    throw new RpaCompileException("Unexpected constraint type");
                            }
                        }
                    }
                    break;

                case HighInstruction.Opcodes.Return:
                case HighInstruction.Opcodes.Branch:
                case HighInstruction.Opcodes.EnterProtectedBlock:
                case HighInstruction.Opcodes.LeaveRegion:
                    break;
                    //throw new NotImplementedException();
                default:
                    throw new ArgumentException();
            }

            if (validationOnly)
                newInstrs.Add(instr);
        }

        private void EmitNumberConversion(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, bool checkOverflow, List<HighInstruction> newInstrs)
        {
            NumberType sourceNumType;
            uint sourcePrecision;
            bool sourceIsUnsigned;
            GetNumberType(src.Type, out sourceNumType, out sourcePrecision, out sourceIsUnsigned);

            NumberType destNumType;
            uint destPrecision;
            bool destIsUnsigned;
            GetNumberType(dest.Type, out destNumType, out destPrecision, out destIsUnsigned);

            HighSsaRegister sourceReg = src;

            if (sourceNumType == NumberType.Int)
            {
                if (sourcePrecision < 32)
                {
                    HighSsaRegister expandedSource = new HighSsaRegister(HighValueType.ValueValue, sourceIsUnsigned ? m_uint32Type : m_int32Type, null);
                    Instructions.RloConvertNumberInstruction.NumConversionType convType = sourceIsUnsigned
                        ? Instructions.RloConvertNumberInstruction.NumConversionType.ZeroExtend
                        : Instructions.RloConvertNumberInstruction.NumConversionType.SignExtend;

                    newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, expandedSource, sourceReg, convType, 32, sourcePrecision));
                    sourceReg = expandedSource;
                    sourcePrecision = 32;
                }

                if (destNumType == NumberType.Int)
                {
                    if (destPrecision > sourcePrecision)
                    {
                        Instructions.RloConvertNumberInstruction.NumConversionType convType = destIsUnsigned
                            ? Instructions.RloConvertNumberInstruction.NumConversionType.ZeroExtend
                            : Instructions.RloConvertNumberInstruction.NumConversionType.SignExtend;
                        newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, dest, sourceReg, convType, destPrecision, sourcePrecision));
                    }
                    else if (destPrecision < sourcePrecision)
                    {
                        Instructions.RloConvertNumberInstruction.NumConversionType convType = destIsUnsigned
                            ? (checkOverflow
                                ? Instructions.RloConvertNumberInstruction.NumConversionType.ZeroTruncate_Checked
                                : Instructions.RloConvertNumberInstruction.NumConversionType.ZeroTruncate)
                            : (checkOverflow
                                ? Instructions.RloConvertNumberInstruction.NumConversionType.SignTruncate_Checked
                                : Instructions.RloConvertNumberInstruction.NumConversionType.SignTruncate);
                        newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, dest, sourceReg, convType, destPrecision, sourcePrecision));
                    }
                    else
                        newInstrs.Add(new Instructions.CopyInstruction(codeLocation, dest, sourceReg));
                }
                else if (destNumType == NumberType.Float)
                {
                    Instructions.RloConvertNumberInstruction.NumConversionType convType = sourceIsUnsigned
                        ? Instructions.RloConvertNumberInstruction.NumConversionType.UIntToFloat
                        : Instructions.RloConvertNumberInstruction.NumConversionType.IntToFloat;
                    newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, dest, sourceReg, convType, destPrecision, sourcePrecision));
                }
                else
                    throw new Exception();
            }
            else if (sourceNumType == NumberType.Float)
            {
                if (destNumType == NumberType.Int)
                {
                    Instructions.RloConvertNumberInstruction.NumConversionType convType = destIsUnsigned
                        ? (checkOverflow
                            ? Instructions.RloConvertNumberInstruction.NumConversionType.FloatToUInt_Checked
                            : Instructions.RloConvertNumberInstruction.NumConversionType.FloatToUInt)
                        : (checkOverflow
                            ? Instructions.RloConvertNumberInstruction.NumConversionType.FloatToInt_Checked
                            : Instructions.RloConvertNumberInstruction.NumConversionType.FloatToInt);
                    newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, dest, sourceReg, convType, destPrecision, sourcePrecision));
                }
                else if (destNumType == NumberType.Float)
                {
                    if (sourcePrecision != destPrecision)
                        newInstrs.Add(new Instructions.RloConvertNumberInstruction(codeLocation, dest, sourceReg, Instructions.RloConvertNumberInstruction.NumConversionType.FloatToFloat, destPrecision, sourcePrecision));
                    else
                        newInstrs.Add(new Instructions.CopyInstruction(codeLocation, dest, sourceReg));
                }
                else
                    throw new Exception();
            }
            else
                throw new Exception();
        }

        private TypeSpecTag SimplifyNumericType(TypeSpecTag typeSpec)
        {
            TypeSpecClassTag result;
            if (m_simplifiedNumberType.TryGetValue(typeSpec, out result))
                return result;

            TypeSpecClassTag classSpec = typeSpec as TypeSpecClassTag;
            if (classSpec != null)
            {
                if (this.Compiler.HaveCliOpenClass(classSpec.TypeName))
                {
                    CliClass cls = this.Compiler.GetClosedClass(classSpec);
                    return SimplifyNumericType(cls.InstanceFields[0].Type);
                }
            }

            throw new RpaCompileException("Couldn't reduce a passive conversion operand to a numeric type");
        }

        private void EmitPassiveValueConversion(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, List<HighInstruction> newInstrs)
        {
            if (dest.Type == src.Type)
            {
                newInstrs.Add(new Instructions.CopyInstruction(codeLocation, dest, src));
                return;
            }

            TypeSpecTag destSimplified = SimplifyNumericType(dest.Type);
            TypeSpecTag srcSimplified = SimplifyNumericType(src.Type);

            if (destSimplified == srcSimplified || (destSimplified == m_nativeIntType && srcSimplified == m_int32Type))
                EmitNumberConversion(codeLocation, dest, src, false, newInstrs);
            else
                throw new RpaCompileException("Unsupported passive numeric conversion");
        }

        private void EmitPassiveRefConversion(CodeLocationTag codeLocation, HighSsaRegister dest, HighSsaRegister src, List<HighInstruction> newInstrs)
        {
            switch (src.ValueType)
            {
                case HighValueType.ConstantString:
                case HighValueType.BoxedValue:
                case HighValueType.Null:
                case HighValueType.ReferenceValue:
                    break;
                default:
                    throw new RpaCompileException("Can't emit passive ref conversion for a non-reference");
            }

            if (dest.ValueType != HighValueType.ReferenceValue)
                throw new RpaCompileException("Can't emit passive ref conversion with a non-reference destination");

            AssignabilityResolver.ConversionType convType = this.Compiler.AssignabilityResolver.ResolveRefAssignable(src.Type, dest.Type);

            switch (convType)
            {
                case AssignabilityResolver.ConversionType.Exact:
                    newInstrs.Add(new Instructions.CopyInstruction(codeLocation, dest, src));
                    break;
                case AssignabilityResolver.ConversionType.ClassToClass:
                    newInstrs.Add(new Instructions.ObjectToObjectInstruction(codeLocation, dest, src));
                    break;
                case AssignabilityResolver.ConversionType.ClassToInterface:
                case AssignabilityResolver.ConversionType.ArrayToGenericCollection:
                case AssignabilityResolver.ConversionType.ArrayToGenericEnumerable:
                case AssignabilityResolver.ConversionType.ArrayToGenericList:
                    newInstrs.Add(new Instructions.ObjectToInterfaceInstruction(codeLocation, dest, src));
                    break;
                case AssignabilityResolver.ConversionType.InterfaceToInterface:
                    newInstrs.Add(new Instructions.InterfaceToInterfaceInstruction(codeLocation, dest, src));
                    break;
                default:
                    convType = this.Compiler.AssignabilityResolver.ResolveRefAssignable(src.Type, dest.Type);
                    throw new RpaCompileException("Implicit conversion failed");
            }
        }

        private void UpdateRefCompare(CodeLocationTag codeLocation, HighSsaRegister[] sources, IList<HighInstruction> newInstrs, out bool isSideConverted)
        {
            foreach (HighSsaRegister src in sources)
            {
                switch (src.ValueType)
                {
                    case HighValueType.BoxedValue:
                    case HighValueType.ConstantString:
                    case HighValueType.Null:
                    case HighValueType.ReferenceValue:
                        break;
                    default:
                        throw new RpaCompileException("Ref comparison operand is not a reference");
                }
            }

            isSideConverted = false;
            if (sources[0].Type != sources[1].Type)
            {
                for (int srcIndex = 0; srcIndex < 2; srcIndex++)
                {
                    HighSsaRegister src = sources[srcIndex];
                    if (src.ValueType == HighValueType.ReferenceValue)
                    {
                        TypeSpecClassTag classTag = src.Type as TypeSpecClassTag;
                        if (classTag != null)
                        {
                            HighTypeDef typeDef = this.Compiler.GetTypeDef(classTag.TypeName);
                            if (typeDef.Semantics == TypeSemantics.Interface)
                            {
                                isSideConverted = true;
                                HighSsaRegister convertedSrc = new HighSsaRegister(HighValueType.ReferenceValue, m_objectType, null);
                                newInstrs.Add(new Instructions.InterfaceToObjectInstruction(codeLocation, convertedSrc, src));

                                sources[srcIndex] = convertedSrc;
                            }
                        }
                    }
                }
            }
        }

        private void GetNumberType(TypeSpecTag typeSpec, out NumberType numType, out uint precision, out bool isUnsigned)
        {
            if (typeSpec == m_float32Type)
            {
                numType = NumberType.Float;
                precision = 32;
                isUnsigned = false;
            }
            else if (typeSpec == m_float64Type)
            {
                numType = NumberType.Float;
                precision = 64;
                isUnsigned = false;
            }
            else if (typeSpec == m_boolType)
            {
                numType = NumberType.Int;
                precision = 8;
                isUnsigned = true;
            }
            else if (typeSpec == m_charType)
            {
                numType = NumberType.Int;
                precision = 16;
                isUnsigned = true;
            }
            else if (typeSpec == m_int8Type)
            {
                numType = NumberType.Int;
                precision = 8;
                isUnsigned = false;
            }
            else if (typeSpec == m_int16Type)
            {
                numType = NumberType.Int;
                precision = 16;
                isUnsigned = false;
            }
            else if (typeSpec == m_int32Type)
            {
                numType = NumberType.Int;
                precision = 32;
                isUnsigned = false;
            }
            else if (typeSpec == m_int64Type)
            {
                numType = NumberType.Int;
                precision = 64;
                isUnsigned = false;
            }
            else if (typeSpec == m_uint8Type)
            {
                numType = NumberType.Int;
                precision = 8;
                isUnsigned = true;
            }
            else if (typeSpec == m_uint16Type)
            {
                numType = NumberType.Int;
                precision = 16;
                isUnsigned = true;
            }
            else if (typeSpec == m_uint32Type)
            {
                numType = NumberType.Int;
                precision = 32;
                isUnsigned = true;
            }
            else if (typeSpec == m_uint64Type)
            {
                numType = NumberType.Int;
                precision = 64;
                isUnsigned = true;
            }
            else if (typeSpec == m_nativeIntType)
            {
                numType = NumberType.Int;
                precision = this.Compiler.Config.NativeIntSizeBits;
                isUnsigned = false;
            }
            else if (typeSpec == m_nativeUIntType)
            {
                numType = NumberType.Int;
                precision = this.Compiler.Config.NativeIntSizeBits;
                isUnsigned = true;
            }
            else
            {
                TypeSpecClassTag classSpec = typeSpec as TypeSpecClassTag;
                if (classSpec != null)
                {
                    HighTypeDef typeDef = this.Compiler.GetTypeDef(classSpec.TypeName);
                    if (typeDef.Semantics == TypeSemantics.Enum)
                    {
                        CliClass cls = this.Compiler.GetClosedClass(classSpec);
                        GetNumberType(cls.InstanceFields[0].Type, out numType, out precision, out isUnsigned);
                        return;
                    }
                }

                throw new RpaCompileException("Unexpected type where number was expected");
            }
        }

        private TypeSpecClassTag ExpectedClassForArithType(NumberArithType arithType)
        {
            switch (arithType)
            {
                case NumberArithType.Float32:
                    return m_float32Type;
                case NumberArithType.Float64:
                    return m_float64Type;
                case NumberArithType.Int32:
                    return m_int32Type;
                case NumberArithType.Int64:
                    return m_int64Type;
                case NumberArithType.NativeInt:
                    return m_nativeIntType;
                case NumberArithType.NativeUInt:
                    return m_nativeUIntType;
                case NumberArithType.UInt32:
                    return m_uint32Type;
                case NumberArithType.UInt64:
                    return m_uint64Type;
                default:
                    throw new ArgumentException();
            }
        }
    }
}
