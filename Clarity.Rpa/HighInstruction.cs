﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public abstract class HighInstruction : ISsaEmitter, ISsaUser
    {
        private CodeLocationTag m_codeLocation;

        private HighCfgEdge m_exceptionEdge;
        private HighCfgEdge m_continuationEdge;

        public delegate void VisitSsaDelegate(ref HighSsaRegister highSsaReg);
        public delegate void VisitCfgEdgeDelegate(ref HighCfgEdge highCfgEdge);
        public delegate void VisitCfgNodeDelegate(ref HighCfgNodeHandle highCfgNode);
        public delegate void VisitLocalDelegate(ref HighLocal highLocal);
        public delegate void VisitTypeSpecDelegate(ref TypeSpecTag typeSpec);
        public delegate void VisitMethodSpecDelegate(ref MethodSpecTag methodSpec);

        public enum Opcodes
        {
            AllocArray,
            AllocObj,
            Arith,
            Box,
            BranchCompareNumbers,
            Branch,
            DynamicCast,
            EnterProtectedBlock,
            ForceDynamicCast,
            GetArrayElementPtr,
            CompareRefs,
            BranchCompareRefs,
            GetStaticFieldAddr,
            BranchRefNull,
            GetTypeInfo,
            CallConstrainedMethod,
            CallConstrainedVirtualMethod,
            LeaveRegion,
            LoadPtr,
            PtrField,
            CallInstanceMethod,
            RefField,
            NumberConvert,
            CallStaticMethod,
            CompareNumbers,
            LoadLocal,
            CallVirtualMethod,
            GetArrayLength,
            PassiveConvert,
            Return,
            GetLocalPtr,
            ReturnValue,
            UnaryArith,
            StoreLocal,
            UnboxPtr,
            ZeroFillPtr,
            UnboxValue,
            Switch,
            Throw,
            StorePtr,
            GetFieldInfo,
            LoadValueField,
            BindStaticDelegate,
            BindInstanceDelegate,
            BindVirtualDelegate,
            Catch,

            FirstInternalOpcode,

            Copy = FirstInternalOpcode,
            BoxNullable,
            CallRloInstanceMethod,
            CallRloVirtualMethod,
            CallRloStaticMethod,
            CallRloInterfaceMethod,

            InterfaceToObject,
            InterfaceToInterface,
            ObjectToObject,
            ObjectToInterface,
            GetRloFieldInfo,
            LoadValueRloField,
            AllocInstanceDelegate,
            RloConvertNumber,
            RloRoutedBranch,
            RloTerminateRoutes,
            CatchOrRoute,
            BranchComparePod,
            ConvertDelegateToMulticast,
            LoadDelegateTarget,
            LoadMulticastDelegateElement,
            GetMulticastDelegateInvocationCount,
        }

        public HighInstruction()
        {
        }

        public HighInstruction(CodeLocationTag codeLocation)
        {
            m_codeLocation = codeLocation;
        }

        public HighInstruction(CodeLocationTag codeLocation, HighCfgNodeHandle exceptionDest, HighCfgNodeHandle continuationDest)
        {
            m_codeLocation = codeLocation;
            m_exceptionEdge = new HighCfgEdge(this, exceptionDest);
            m_continuationEdge = new HighCfgEdge(this, continuationDest);
        }

        public void VisitAllSsa(VisitSsaDelegate visitor)
        {
            this.VisitSsaDests(visitor);
            this.VisitSsaUses(visitor);
        }

        public void WriteDisassembly(CfgWriter cw, DisassemblyWriter dw)
        {
            dw.Write(this.Opcode.ToString());
            dw.Write(" ");
            this.WriteDisassemblyImpl(cw, dw);

            this.VisitSsaDests(delegate (ref HighSsaRegister dest)
            {
                dw.Write(" ");
                cw.WriteDefSsa(dw, dest);
            });
            this.VisitSsaUses(delegate (ref HighSsaRegister use)
            {
                dw.Write(" ");
                cw.WriteUseSsa(dw, use);
            });

            ILocalUsingInstruction localUsing = this as ILocalUsingInstruction;
            if (localUsing != null)
            {
                localUsing.VisitLocalRefs(delegate (ref HighLocal local)
                {
                    dw.Write(" ");
                    cw.WriteLocal(dw, local);
                });
            }

            IBranchingInstruction branching = this as IBranchingInstruction;
            if (branching != null)
            {
                branching.VisitSuccessors(delegate (ref HighCfgEdge cfgEdge)
                {
                    dw.Write(" ");
                    cw.WriteEdge(dw, cfgEdge);
                });
            }

            if (this.MayThrow)
            {
                dw.PushIndent();
                if (this.ContinuationEdge != null)
                {
                    dw.WriteLine("");
                    dw.Write("continueTo ");
                    cw.WriteEdge(dw, this.ContinuationEdge);
                }
                if (this.ExceptionEdge != null)
                {
                    dw.WriteLine("");
                    dw.Write("throwTo ");
                    cw.WriteEdge(dw, this.ExceptionEdge);
                }
                dw.PopIndent();
            }
            dw.WriteLine("");
        }

        public CodeLocationTag CodeLocation
        {
            get { return m_codeLocation; }
            set { m_codeLocation = value; }
        }

        public abstract void VisitSsaDests(VisitSsaDelegate visitor);
        public abstract void VisitSsaUses(VisitSsaDelegate visitor);
        public abstract void WriteHeader(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer);
        public abstract void ReadHeader(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader);
        public abstract Opcodes Opcode { get; }

        protected abstract void WriteDisassemblyImpl(CfgWriter cw, DisassemblyWriter dw);
        protected abstract HighInstruction CloneImpl();

        public virtual bool TerminatesControlFlow { get { return false; } }
        public abstract bool MayThrow { get; }

        public HighCfgEdge ExceptionEdge { get { return m_exceptionEdge; } set { m_exceptionEdge = value; } }
        public HighCfgEdge ContinuationEdge { get { return m_continuationEdge; } set { m_continuationEdge = value; } }

        public void Write(HighFileBuilder fileBuilder, HighMethodBuilder methodBuilder, HighRegionBuilder regionBuilder, HighCfgNodeBuilder cfgNodeBuilder, bool haveDebugInfo, BinaryWriter writer)
        {
            writer.Write((byte)this.Opcode);
            if (haveDebugInfo)
                writer.Write(CodeLocation.Offset);

            this.WriteHeader(fileBuilder, methodBuilder, regionBuilder, cfgNodeBuilder, haveDebugInfo, writer);

            this.VisitSsaUses(delegate (ref HighSsaRegister use)
            {
                writer.Write(cfgNodeBuilder.GetSsaID(use));
            });

            this.VisitSsaDests(delegate (ref HighSsaRegister dest)
            {
                HighSsaRegister destValue = dest;
                if (destValue == null)
                    writer.Write(false);
                else
                {
                    writer.Write(true);
                    cfgNodeBuilder.AddSsa(destValue);
                    destValue.WriteDestinationDef(fileBuilder, regionBuilder, writer);
                }
            });

            ILocalUsingInstruction localUsing = this as ILocalUsingInstruction;
            if (localUsing != null)
            {
                localUsing.VisitLocalRefs(delegate (ref HighLocal local)
                {
                    writer.Write(methodBuilder.LookupLocal(local));
                });
            }

            IBranchingInstruction branching = this as IBranchingInstruction;
            if (branching != null)
            {
                branching.VisitSuccessors(delegate (ref HighCfgEdge cfgEdge)
                {
                    writer.Write(regionBuilder.IndexCfgNode(cfgEdge.Dest.Value));
                });
            }
        }

        public static HighInstruction Read(TagRepository rpa, CatalogReader catalog, HighMethodBodyParseContext methodBody, HighCfgNodeHandle[] cfgNodes, List<HighSsaRegister> ssaRegisters, CodeLocationTag baseLocation, bool haveDebugInfo, BinaryReader reader)
        {
            Opcodes opcode = (Opcodes)reader.ReadByte();

            CodeLocationTag codeLocation = baseLocation;
            if (haveDebugInfo)
                codeLocation = new CodeLocationTag(baseLocation.MethodDecl, reader.ReadUInt32());

            HighInstruction instr;

            switch (opcode)
            {
                case Opcodes.AllocArray:
                    instr = new Instructions.AllocArrayInstruction();
                    break;
                case Opcodes.AllocObj:
                    instr = new Instructions.AllocObjInstruction();
                    break;
                case Opcodes.Arith:
                    instr = new Instructions.ArithInstruction();
                    break;
                case Opcodes.Box:
                    instr = new Instructions.BoxInstruction();
                    break;
                case Opcodes.BranchCompareNumbers:
                    instr = new Instructions.BranchCompareNumbersInstruction();
                    break;
                case Opcodes.Branch:
                    instr = new Instructions.BranchInstruction();
                    break;
                case Opcodes.DynamicCast:
                    instr = new Instructions.DynamicCastInstruction();
                    break;
                case Opcodes.EnterProtectedBlock:
                    instr = new Instructions.EnterProtectedBlockInstruction();
                    break;
                case Opcodes.ForceDynamicCast:
                    instr = new Instructions.ForceDynamicCastInstruction();
                    break;
                case Opcodes.GetArrayElementPtr:
                    instr = new Instructions.GetArrayElementPtrInstruction();
                    break;
                case Opcodes.CompareRefs:
                    instr = new Instructions.CompareRefsInstruction();
                    break;
                case Opcodes.BranchCompareRefs:
                    instr = new Instructions.BranchCompareRefsInstruction();
                    break;
                case Opcodes.GetStaticFieldAddr:
                    instr = new Instructions.GetStaticFieldAddrInstruction();
                    break;
                case Opcodes.BranchRefNull:
                    instr = new Instructions.BranchRefNullInstruction();
                    break;
                case Opcodes.GetTypeInfo:
                    instr = new Instructions.GetTypeInfoInstruction();
                    break;
                case Opcodes.CallConstrainedMethod:
                    instr = new Instructions.CallConstrainedMethodInstruction();
                    break;
                case Opcodes.CallConstrainedVirtualMethod:
                    instr = new Instructions.CallConstrainedVirtualMethodInstruction();
                    break;
                case Opcodes.LeaveRegion:
                    instr = new Instructions.LeaveRegionInstruction();
                    break;
                case Opcodes.LoadPtr:
                    instr = new Instructions.LoadPtrInstruction();
                    break;
                case Opcodes.PtrField:
                    instr = new Instructions.PtrFieldInstruction();
                    break;
                case Opcodes.CallInstanceMethod:
                    instr = new Instructions.CallInstanceMethodInstruction();
                    break;
                case Opcodes.RefField:
                    instr = new Instructions.RefFieldInstruction();
                    break;
                case Opcodes.NumberConvert:
                    instr = new Instructions.NumberConvertInstruction();
                    break;
                case Opcodes.CallStaticMethod:
                    instr = new Instructions.CallStaticMethodInstruction();
                    break;
                case Opcodes.CompareNumbers:
                    instr = new Instructions.CompareNumbersInstruction();
                    break;
                case Opcodes.LoadLocal:
                    instr = new Instructions.LoadLocalInstruction();
                    break;
                case Opcodes.CallVirtualMethod:
                    instr = new Instructions.CallVirtualMethodInstruction();
                    break;
                case Opcodes.GetArrayLength:
                    instr = new Instructions.GetArrayLengthInstruction();
                    break;
                case Opcodes.PassiveConvert:
                    instr = new Instructions.PassiveConvertInstruction();
                    break;
                case Opcodes.Return:
                    instr = new Instructions.ReturnInstruction();
                    break;
                case Opcodes.GetLocalPtr:
                    instr = new Instructions.GetLocalPtrInstruction();
                    break;
                case Opcodes.ReturnValue:
                    instr = new Instructions.ReturnValueInstruction();
                    break;
                case Opcodes.UnaryArith:
                    instr = new Instructions.UnaryArithInstruction();
                    break;
                case Opcodes.StoreLocal:
                    instr = new Instructions.StoreLocalInstruction();
                    break;
                case Opcodes.UnboxPtr:
                    instr = new Instructions.UnboxPtrInstruction();
                    break;
                case Opcodes.ZeroFillPtr:
                    instr = new Instructions.ZeroFillPtrInstruction();
                    break;
                case Opcodes.UnboxValue:
                    instr = new Instructions.UnboxValueInstruction();
                    break;
                case Opcodes.Switch:
                    instr = new Instructions.SwitchInstruction();
                    break;
                case Opcodes.Throw:
                    instr = new Instructions.ThrowInstruction();
                    break;
                case Opcodes.StorePtr:
                    instr = new Instructions.StorePtrInstruction();
                    break;
                case Opcodes.GetFieldInfo:
                    instr = new Instructions.GetFieldInfoInstruction();
                    break;
                case Opcodes.LoadValueField:
                    instr = new Instructions.LoadValueFieldInstruction();
                    break;
                case Opcodes.BindStaticDelegate:
                    instr = new Instructions.BindStaticDelegateInstruction();
                    break;
                case Opcodes.BindInstanceDelegate:
                    instr = new Instructions.BindInstanceDelegateInstruction();
                    break;
                case Opcodes.BindVirtualDelegate:
                    instr = new Instructions.BindVirtualDelegateInstruction();
                    break;
                case Opcodes.Catch:
                    instr = new Instructions.CatchInstruction();
                    break;
                default:
                    throw new Exception("Invalid opcode");
            }

            instr.ReadHeader(rpa, catalog, methodBody, cfgNodes, ssaRegisters, baseLocation, haveDebugInfo, reader);

            instr.VisitSsaUses(delegate (ref HighSsaRegister use)
            {
                uint regIndex = reader.ReadUInt32();
                use = ssaRegisters[(int)regIndex];
            });

            instr.VisitSsaDests(delegate (ref HighSsaRegister dest)
            {
                if (reader.ReadBoolean())
                {
                    HighSsaRegister destV = HighSsaRegister.ReadDestinationDef(rpa, catalog, reader);
                    ssaRegisters.Add(destV);
                    dest = destV;
                }
                else
                    dest = null;
            });

            IExtractableTypesInstruction extractableTypes = instr as IExtractableTypesInstruction;
            if (extractableTypes != null)
                extractableTypes.ExtractSsaTypes();

            ILocalUsingInstruction localUsing = instr as ILocalUsingInstruction;
            if (localUsing != null)
            {
                localUsing.VisitLocalRefs(delegate (ref HighLocal local)
                {
                    local = methodBody.GetLocal(reader.ReadUInt32());
                });
            }

            IBranchingInstruction branching = instr as IBranchingInstruction;
            if (branching != null)
            {
                branching.VisitSuccessors(delegate (ref HighCfgEdge cfgEdge)
                {
                    cfgEdge = new HighCfgEdge(instr, cfgNodes[reader.ReadUInt32()]);
                });
            }

            instr.CodeLocation = codeLocation;

            return instr;
        }

        public HighInstruction Clone()
        {
            HighInstruction clone = this.CloneImpl();
            if (m_continuationEdge != null)
                clone.m_continuationEdge = new HighCfgEdge(clone, m_continuationEdge.Dest);
            if (m_exceptionEdge != null)
                clone.m_exceptionEdge = new HighCfgEdge(clone, m_exceptionEdge.Dest);
            return clone;
        }
    }
}
