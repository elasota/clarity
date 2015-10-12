using System;
using System.Collections.Generic;
using AssemblyImporter.CLR;

namespace AssemblyImporter.CppExport
{
    public class CfgNodeEdge
    {
        public SsaRegister[] Registers { get; private set; }

        public CfgNodeEdge(SsaRegister[] regs)
        {
            Registers = regs;
        }
    }

    public class CfgNode
    {
        public CfgBuilder CfgBuilder { get { return m_builder; } }
        public int StartInstr { get { return m_startInstr; } }
        public VType[] EntryTypes { get { return m_entryTypes; } }
        public MidInstruction[] MidInstructions { get { return m_midInstructions; } }
        public IList<CfgOutboundEdge> Successors { get { return m_successors; } }
        public IList<CfgNode> Predecessors { get { return m_predecessors; } }
        public CfgOutboundEdge FallThroughEdge { get { return m_fallThroughEdge; } }

        public bool CanBeContinuous
        {
            get
            {
                if (FallThroughEdge == null)
                    return false;
                if (FallThroughEdge.SuccessorNode.Predecessors.Count != 1)
                    return false;
                VType[] exitTypes = FallThroughEdge.OutputValueTypes;
                VType[] entryTypes = FallThroughEdge.SuccessorNode.EntryTypes;
                for (int vti = 0; vti < exitTypes.Length; vti++)
                {
                    if (!exitTypes[vti].Equals(entryTypes[vti]))
                        return false;
                }
                return true;
            }
        }

        private VType[] m_entryTypes;
        private CfgBuilder m_builder;
        private int m_startInstr;
        private MidInstruction[] m_midInstructions;
        private List<CfgOutboundEdge> m_successors = new List<CfgOutboundEdge>();
        private List<CfgNode> m_predecessors = new List<CfgNode>();

        public CfgNode(CfgBuilder builder, int startInstr, VType[] entryTypes)
        {
            m_builder = builder;
            m_startInstr = startInstr;
            m_entryTypes = entryTypes;
        }

        public void UpdateEntryTypes(CfgNodeCompiler nodeCompiler, VType[] newEntryTypes, out bool outNeedsReparse)
        {
            int numVTypes = newEntryTypes.Length;
            if (numVTypes != m_entryTypes.Length)
                throw new ArgumentException("Mismatched CFG edge");

            bool anyDifferent = false;
            for (int i = 0; i < numVTypes; i++)
            {
                if (!newEntryTypes[i].Equals(m_entryTypes[i]))
                {
                    anyDifferent = true;
                    break;
                }
            }

            if (!anyDifferent)
                outNeedsReparse = false;
            else
            {
                outNeedsReparse = true;

                VType[] newTypes = new VType[m_entryTypes.Length];
                for (int i = 0; i < numVTypes; i++)
                    newTypes[i] = ConvergeVTypes(nodeCompiler, m_entryTypes[i], newEntryTypes[i]);
                m_entryTypes = newTypes;
            }
        }

        private class ValTypeConvergence
        {
            private VType.ValTypeEnum m_source1;
            private VType.ValTypeEnum m_source2;
            private VType.ValTypeEnum m_converged;

            public ValTypeConvergence(VType.ValTypeEnum source1, VType.ValTypeEnum source2, VType.ValTypeEnum converged)
            {
                m_source1 = source1;
                m_source2 = source2;
                m_converged = converged;
            }

            public bool TryConverge(VType.ValTypeEnum source1, VType.ValTypeEnum source2, ref VType.ValTypeEnum converged)
            {
                if ((source1 == m_source1 && source2 == m_source2) || (source2 == m_source1 && source1 == m_source2))
                {
                    converged = m_converged;
                    return true;
                }
                return false;
            }
        }

        private static ValTypeConvergence[] InitConvergences()
        {
            List<ValTypeConvergence> convergences = new List<ValTypeConvergence>();
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.ConstantReference, VType.ValTypeEnum.ConstantReference, VType.ValTypeEnum.NotNullReferenceValue));
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.ConstantReference, VType.ValTypeEnum.NotNullReferenceValue, VType.ValTypeEnum.NotNullReferenceValue));
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.ConstantReference, VType.ValTypeEnum.NullableReferenceValue, VType.ValTypeEnum.NullableReferenceValue));
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.ConstantValue, VType.ValTypeEnum.ConstantValue, VType.ValTypeEnum.ValueValue));
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.ConstantValue, VType.ValTypeEnum.ValueValue, VType.ValTypeEnum.ValueValue));
            convergences.Add(new ValTypeConvergence(VType.ValTypeEnum.NotNullReferenceValue, VType.ValTypeEnum.NullableReferenceValue, VType.ValTypeEnum.NullableReferenceValue));

            return convergences.ToArray();
        }

        public void AddPredecessor(CfgNode outboundEdge)
        {
            m_predecessors.Add(outboundEdge);
        }

        public void AddSuccessor(CfgOutboundEdge outboundEdge)
        {
            m_successors.Add(outboundEdge);
        }

        private static ValTypeConvergence[] ms_convergences = InitConvergences();
        private CfgOutboundEdge m_fallThroughEdge;

        private VType ConvergeVTypes(CfgNodeCompiler nodeCompiler, VType vType1, VType vType2)
        {
            VType.ValTypeEnum convergedValType = VType.ValTypeEnum.Invalid;

            if (vType1.Equals(vType2))
                return vType1;

            if (vType1.ValType == VType.ValTypeEnum.Null)
            {
                if (vType2.ValType == VType.ValTypeEnum.NotNullReferenceValue || vType2.ValType == VType.ValTypeEnum.NullableReferenceValue || vType2.ValType == VType.ValTypeEnum.ConstantReference)
                    return new VType(VType.ValTypeEnum.NullableReferenceValue, vType1.TypeSpec);
                else
                    throw new ArgumentException();  // Both null should be equal
            }

            if (vType2.ValType == VType.ValTypeEnum.Null)
            {
                if (vType1.ValType == VType.ValTypeEnum.NotNullReferenceValue || vType1.ValType == VType.ValTypeEnum.NullableReferenceValue || vType1.ValType == VType.ValTypeEnum.ConstantReference)
                    return new VType(VType.ValTypeEnum.NullableReferenceValue, vType2.TypeSpec);
                else
                    throw new ArgumentException();  // Both null should be equal
            }

            if (vType1.ValType == vType2.ValType)
            {
                convergedValType = vType1.ValType;

                if (convergedValType == VType.ValTypeEnum.ConstantValue)
                    convergedValType = VType.ValTypeEnum.ValueValue;
                else if (convergedValType == VType.ValTypeEnum.ConstantReference)
                    convergedValType = VType.ValTypeEnum.NotNullReferenceValue;
            }
            else
            {
                // Different val types
                bool anyMatched = false;
                foreach (ValTypeConvergence convergence in ms_convergences)
                    anyMatched = anyMatched || convergence.TryConverge(vType1.ValType, vType2.ValType, ref convergedValType);

                if (!anyMatched)
                    throw new ArgumentException();
            }

            // Same value type?
            if (vType1.TypeSpec.Equals(vType2.TypeSpec))
                return new VType(convergedValType, vType1.TypeSpec);

            // Different value types
            if (convergedValType == VType.ValTypeEnum.ValueValue)
            {
                // Both are value types, so these must converge
                return new VType(convergedValType, nodeCompiler.ArithConvergeValues(vType1.TypeSpec, vType2.TypeSpec));
            }

            if (convergedValType == VType.ValTypeEnum.NotNullReferenceValue || convergedValType == VType.ValTypeEnum.NullableReferenceValue)
            {
                // Both are reference types
                // The merge rules are (where vType1 is the existing type):
                // - If vType2 is assignable to vType1, use vType1
                // - If vType1 is assignable to vType2, use vType2
                // - Otherwise, use closest common supertype
                CppAssignabilityResolver resolver = new CppAssignabilityResolver(nodeCompiler.CppBuilder, nodeCompiler.CommonTypeLookupInst, m_builder.InClass, m_builder.InMethod);
                if (resolver.IsRefAssignable(vType2.TypeSpec, vType1.TypeSpec))
                    return new VType(convergedValType, vType1.TypeSpec);
                if (resolver.IsRefAssignable(vType1.TypeSpec, vType2.TypeSpec))
                    return new VType(convergedValType, vType2.TypeSpec);

                CLRTypeSpec commonBase = resolver.FindCommonBase(vType1.TypeSpec, vType2.TypeSpec);
                if (commonBase == null)
                    throw new ParseFailedException("CFG edge merge failed");

                return new VType(convergedValType, commonBase);
            }

            throw new ParseFailedException("CFG edge merge failed");
        }

        public void Parse()
        {
            CfgNodeCompiler compiler = new CfgNodeCompiler(this);
            compiler.Compile();

            m_midInstructions = compiler.OutputInstructions;
            m_fallThroughEdge = compiler.OutputFallThroughEdge;
        }
    }
}
