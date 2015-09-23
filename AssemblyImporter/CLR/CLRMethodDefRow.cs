using System;

namespace AssemblyImporter.CLR
{
    // II.22.26
    public class CLRMethodDefRow : CLRTableRow, ICLROwnedBy<CLRTypeDefRow>
    {
        public enum MethodMemberAccess
        {
            CompilerControlled = 0x0,
            Private = 0x1,
            FamilyAndAssembly = 0x2,
            Assembly = 0x3,
            Family = 0x4,
            FamilyOrAssembly = 0x5,
            Public = 0x6,
        }

        public enum MethodVtableLayout
        {
            ReuseSlot = 0x000,
            NewSlot = 0x100,
        }

        public enum MethodCodeType
        {
            IL = 0x0,
            Native = 0x1,
            OPTIL = 0x2,
            Runtime = 0x3,
        }

        public enum MethodManaged
        {
            Unmanaged = 0x4,
            Managed  = 0x0,
        }

        public uint RVA { get; private set; }   // Points to COR_ILMETHOD
        public string Name { get; private set; }
        public CLRParamRow[] ParamList { get; private set; }
        public MethodMemberAccess MemberAccess { get; private set; }
        public bool Static { get; private set; }
        public bool Final { get; private set; }
        public bool Virtual { get; private set; }
        public bool HideBySig { get; private set; }
        public MethodVtableLayout VtableLayout { get; private set; }
        public bool Strict { get; private set; }
        public bool Abstract { get; private set; }
        public bool SpecialName { get; private set; }
        public bool PInvokeImpl { get; private set; }
        public bool UnmanagedExport { get; private set; }
        public bool RTSpecialName { get; private set; }
        public bool HasSecurity { get; private set; }
        public bool RequireSecObject { get; private set; }

        public bool ForwardRef { get; private set; }
        public bool PreserveSig { get; private set; }
        public bool InternalCall { get; private set; }
        public bool Synchronized { get; private set; }
        public bool NoInlining { get; private set; }
        public bool NoOptimization { get; private set; }

        public MethodCodeType CodeType { get; private set; }
        public MethodManaged Managed { get; private set; }
        public CLRSigMethodDefOrRefSig Signature { get; private set; }
        public CLRGenericParamRow[] GenericParameters { get; private set; }

        public CIL.Method Method { get; private set; }

        public CLRTypeDefRow Owner { get; set; }

        private uint m_firstParam;

        public override void Parse(CLRMetaDataParser parser)
        {
            RVA = parser.ReadU32();
            uint implFlags = parser.ReadU16();
            uint flags = parser.ReadU16();

            uint memberAccess = (flags & 0x7);
            if (memberAccess > 0x6)
                throw new ParseFailedException("Invalid member access");
            MemberAccess = (MethodMemberAccess)memberAccess;
            Static = ((flags & 0x10) != 0);
            Final = ((flags & 0x20) != 0);
            Virtual = ((flags & 0x40) != 0);
            HideBySig = ((flags & 0x80) != 0);
            VtableLayout = (MethodVtableLayout)(flags & 0x100);
            
            Strict = ((flags & 0x200) != 0);
            Abstract = ((flags & 0x400) != 0);
            SpecialName = ((flags & 0x800) != 0);
            PInvokeImpl = ((flags & 0x2000) != 0);
            UnmanagedExport = ((flags & 0x8) != 0);
            RTSpecialName = ((flags & 0x1000) != 0);
            HasSecurity = ((flags & 0x4000) != 0);
            RequireSecObject = ((flags & 0x8000) != 0);

            Name = parser.ReadString();

            if (Name == "TestGenericMethod")
            {
                int bp = 0;
            }

            Signature = new CLRSigMethodDefOrRefSig(new CLRSignatureParser(parser.ReadBlob(), parser.Tables), CLRSigMethodDefOrRefSig.Kind.Def);

            CodeType = (MethodCodeType)(implFlags & 0x3);
            Managed = (MethodManaged)(implFlags & 0x4);

            ForwardRef = ((flags & 0x10) != 0);
            PreserveSig = ((flags & 0x80) != 0);
            InternalCall = ((flags & 0x1000) != 0);
            Synchronized = ((flags & 0x20) != 0);
            NoInlining = ((flags & 0x8) != 0);
            NoOptimization = ((flags & 0x40) != 0);

            m_firstParam = parser.ReadTableRawRow(CLRMetaDataTables.TableIndex.Param);
        }

        public override void ResolveSpans(CLRTableRow nextRow, CLRMetaDataParser parser)
        {
            ParamList = CLRSpanResolver<CLRMethodDefRow, CLRParamRow>.Resolve(this, nextRow, ref m_firstParam, parser,
                CLRMetaDataTables.TableIndex.Param, nextRowTyped => { return nextRowTyped.m_firstParam; } );
        }

        public override bool AllowEmptyTable()
        {
            return true;
        }

        public override void Validate()
        {
            // Owned by only one typedef
            // ImplFlags restricted
            // Flags restricted
            // If Name is ".ctor", method marked SpecialName, and no rows in GenericParam point to this
            // Bad flag mixes:
            //     Static + Final
            //     Static + Virtual
            //     Static + NewSlot
            //     Final + Abstract
            //     Abstract + PinvokeImpl
            //     CompilerControlled + SpecialName
            //     CompilerControlled + RTSPecialName
            // If Abstract then Virtual
            // If RTSpecialName then SpecialName
            // If HasSecurity, either:
            //     Owns at least 1 row in DeclSecurity
            //     Has SuppressUnmanagedCodeSecurityAttribute
            // If either of above, HasSecurity = 1
            // Name is not empty
            // If name is .ctor, not interface
            // If not RTSpecialName, Name is valid CLS identifier
            // If CompilerControlled, dupe checking ignored
            // Not owned by <Module>
            // If owner is ValueType, Synchronized = 0
            // No duplicates based on owner+Name+Signature, return type ignored
            // If Final, NewSlot, or Strict, then Virtual
            // If PInvokeImpl, not Virtual
            // If not Abstract, then one of: RVA non-zero, PInvokeImpl, or Runtime
            // If CompilerControlled, RVA is non-zero or PinvokeImpl
            // Signature must be DEFAULT or GENERIC
            // If not Static, then signature has HASTHIS
            // If Static, signature does not have HASTHIS
            // If EXPLICITTHIS, then HASTHIS set
            // If EXPLICITTHIS, signature preceded by FNPTR
            // If RVA = 0, then one of: Abstract, Runtime or PinvokeImpl
            // If RVA != 0, then:
            //     Not Abstract
            //     CodeType is Native, CIL, or Runtime
            //     RVA points into CIL code stream
            // If PinvokeImpl, RVA = 0 and owns a row in ImplMap
            // If RTSpecialName, then Name is .cctor or .ctor
            // If Name is .cctor or .ctor, RTSpecialName set
            // If Name is .ctor:
            //     Return type is ELEMENT_TYPE_VOID
            //     Not Static, not Abstract, not Virtual
            //     Owner is Class or ValueType
            // If Name is .cctor:
            //     Return type is ELEMENT_TYPE_VOID
            //     Signature convention is DEFAULT
            //     No parameters in signature
            //     Static, not Virtual, not Abstract
            // At most one .cctor
        }

        public void DigestMethod(CLRMetaDataParser parser)
        {
            this.Method = new CIL.Method(parser);
        }

        public void AddGenericParameter(CLRGenericParamRow genericParam)
        {
            int oldParamLength = 0;
            if (GenericParameters != null)
                oldParamLength = GenericParameters.Length;

            int newLength = genericParam.Number + 1;
            if (newLength > oldParamLength)
            {
                CLRGenericParamRow[] newParams = new CLRGenericParamRow[newLength];
                for (int i = 0; i < oldParamLength; i++)
                    newParams[i] = GenericParameters[i];
                GenericParameters = newParams;
            }
            GenericParameters[genericParam.Number] = genericParam;
        }
    }
}
