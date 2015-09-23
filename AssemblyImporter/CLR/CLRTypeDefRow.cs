using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRTypeDefRow : CLRTableRow
    {
        public enum TypeVisibility
        {
            NotPublic = 0,
            Public = 1,
            NestedPublic = 2,
            NestedPrivate = 3,
            NestedFamily = 4,
            NestedAssembly = 5,
            NestedFamilyAndAssembly = 6,
            NestedFamilyOrAssembly = 7,
        }

        public enum TypeClassLayout
        {
            AutoLayout = 0,
            SequentialLayout = 0x8,
            ExplicitLayout = 0x10,
        };

        public enum TypeSemantics
        {
            Class = 0,
            Interface = 0x20,
        }

        public enum TypeStringFormat
        {
            AnsiClass = 0x0,
            UnicodeClass = 0x10000,
            AutoClass = 0x20000,
            CustomFormatClass = 0x30000,
        }

        public TypeVisibility Visibility { get; private set; }
        public TypeClassLayout ClassLayout { get; private set; }
        public TypeSemantics Semantics { get; private set; }
        public TypeStringFormat StringFormat { get; private set; }
        public string TypeName { get; private set; }
        public string TypeNamespace { get; private set; }
        public CLRTableRow Extends { get; private set; }
        public bool IsAbstract { get; private set; }
        public bool IsSealed { get; private set; }
        public bool IsSpecialName { get; private set; }
        public bool IsImported { get; private set; }
        public bool IsSerializable { get; private set; }
        public byte CustomStringFormat { get; private set; }
        public bool IsBeforeFieldInit { get; private set; }
        public bool IsRTSpecialName { get; private set; }
        public bool HasSecurity { get; private set; }
        public bool IsTypeForwarder { get; private set; }
        public CLRFieldRow[] Fields { get; private set; }
        public CLRMethodDefRow[] MethodDefs { get; private set; }

        public IEnumerable<CLRTypeDefRow> EnclosedClasses { get { return m_childClasses; } }
        public IEnumerable<CLRTableRow> ImplementedInterfaces { get { return m_implementedInterfaces; } }
        public IEnumerable<CLRMethodImplRow> MethodImplementations { get { return m_methodImpls; } }
        public CLRGenericParamRow[] GenericParameters { get; private set; }
        public CLRTypeDefRow ContainerClass { get; private set; }

        private uint m_firstField;
        private uint m_firstMethodDef;
        private List<CLRTypeDefRow> m_childClasses;
        private List<CLRTableRow> m_implementedInterfaces;
        private List<CLRMethodImplRow> m_methodImpls;

        public override void Parse(CLRMetaDataParser parser)
        {
            m_childClasses = new List<CLRTypeDefRow>();
            m_implementedInterfaces = new List<CLRTableRow>();
            m_methodImpls = new List<CLRMethodImplRow>();

            GenericParameters = null;

            uint flags = parser.ReadU32();

            uint vis = flags & 0x07;
            if (vis > 0x07)
                throw new ParseFailedException("Invalid visibility");

            uint layout = flags & 0x18;
            if (layout != 0x0 && layout != 0x08 && layout != 0x10)
                throw new ParseFailedException("Invalid layout");

            uint semantics = flags & 0x20;
            if (semantics != 0 && semantics != 0x20)
                throw new ParseFailedException("Invalid semantics");

            Visibility = (TypeVisibility)vis;
            ClassLayout = (TypeClassLayout)layout;
            Semantics = (TypeSemantics)semantics;
            IsAbstract = (flags & 0x80) != 0;
            IsSealed = (flags & 0x100) != 0;
            IsSpecialName = (flags & 0x400) != 0;
            IsImported = (flags & 0x1000) != 0;
            IsSerializable = (flags & 0x2000) != 0;
            StringFormat = (TypeStringFormat)(flags & 0x30000);
            CustomStringFormat = (byte)((flags & 0xc00000) >> 22);
            IsBeforeFieldInit = (flags & 0x100000) != 0;
            IsRTSpecialName = (flags & 0x800) != 0;
            HasSecurity = (flags & 0x40000) != 0;
            IsTypeForwarder = (flags & 0x200000) != 0;

            TypeName = parser.ReadString();
            TypeNamespace = parser.ReadString();
            Extends = parser.ReadTypeDefOrRefOrSpec();
            m_firstField = parser.ReadTableRawRow(CLRMetaDataTables.TableIndex.Field);
            m_firstMethodDef = parser.ReadTableRawRow(CLRMetaDataTables.TableIndex.MethodDef);

            if (m_firstField == 0 || m_firstMethodDef == 0)
                throw new ParseFailedException("Invalid method/field def span");
        }

        public override void ResolveSpans(CLRTableRow nextRow, CLRMetaDataParser parser)
        {
            Fields = CLRSpanResolver<CLRTypeDefRow, CLRFieldRow>.Resolve(this, nextRow, ref m_firstField, parser,
                CLRMetaDataTables.TableIndex.Field, nextRowTyped => { return nextRowTyped.m_firstField; });
            MethodDefs = CLRSpanResolver<CLRTypeDefRow, CLRMethodDefRow>.Resolve(this, nextRow, ref m_firstMethodDef, parser,
                CLRMetaDataTables.TableIndex.MethodDef, nextRowTyped => { return nextRowTyped.m_firstMethodDef; });
        }

        public override bool AllowEmptyTable()
        {
            return false;
        }

        public override void Validate()
        {
            //CLARITYTODO:
            // Classes and ValueTypes must derive from System.Object
            // Delegates must derive directly from System.Delegate
            // Force flags
            // If HasSecurity:
            //    Owns at least 1 row in DeclSecurity
            //    Has custom attrib SuppressUnmanagedCodeSecurityAttribute
            // If owns a row in DeclSecurity or has SuppressUnmanagedCodeSecurityAttribute
            //    HasSecurity = true
            // Name is non-empty
            // TypeName is a valid CLS identifier
            // If TypeNamespace is not null:
            //     TypeNamespace is a valid CLS identifier and not empty
            // System.Object extends null
            // System.ValueType extends System.Object
            // Except for System.Object and <Module>, all classes extend a non-sealed class
            // Non-circular extension
            // Interfaces do not extend
            // FieldList can be null
            // MethodList can be null
            // Type can own zero or more methods
            // Runtime size of a value type can not exceed 1MB
            // If MethodList, must index a valid row
            // If any methods are abstract, class must be abstract (requires runtime check)
            // Interface must be abstract
            // Abstract types may have .ctor
            // Abstract types must implement all classes
            // Interfaces can own static fields, but not instance fields
            // Interface can not be sealed
            // Interface methods must be abstract
            // No duplicate rows where TypeNamespace+TypeName(+OwnerRowInNestedClassTable if nested) is non-unique
            // No duplicate rows where TypeNamespace+TypeName(+OwnerRowInNestedClassTable if nested) are same using CLS conflicting identifier rules
            // Enums:
            //     Must be sealed
            //     No methods
            //     No interfaces
            //     No properties
            //     No events
            //     Static fields must be Literal
            //     One or more static, literal fields, all have type Enum
            //     One instance field of built-in integer type
            //     Name of instance field must be "value__" and marked RTSpecialName
            // Nested type must own exactly one NestedClass
            // ValueType is sealed
        }

        public void AddChildClass(CLRTypeDefRow childClass)
        {
            childClass.ContainerClass = this;
            m_childClasses.Add(childClass);
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

        public void AddInterfaceImplementation(CLRTableRow ii)
        {
            m_implementedInterfaces.Add(ii);
        }

        public void AddMethodImplementation(CLRMethodImplRow mi)
        {
            m_methodImpls.Add(mi);
        }
    }
}
