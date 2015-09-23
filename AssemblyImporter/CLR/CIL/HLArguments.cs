using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR.CIL
{
    public struct HLArguments
    {
        public class No
        {
            public bool TypeCheck { get; private set; }
            public bool RangeCheck { get; private set; }
            public bool NullCheck { get; private set; }

            public No(byte baseByte)
            {
                TypeCheck = ((baseByte & 0x01) != 0);
                RangeCheck = ((baseByte & 0x02) != 0);
                NullCheck = ((baseByte & 0x04) != 0);
            }
        }

        public enum ArgsTypeEnum
        {
            Unknown,

            U32,
            I32,
            U64,
            I64,
            F32,
            F64,

            String,

            No,
            Switch,
            MetaToken,
        }

        public ulong U64Value { get { return m_rawInt; } }
        public uint U32Value { get { return (uint)U64Value; } }
        public long S64Value { get { return (long)m_rawInt; } }
        public int S32Value { get { return (int)S64Value; } }
        public double F64Value { get { return m_rawFloat; } }
        public float F32Value { get { return (float)m_rawFloat; } }
        public object ObjValue { get { return m_objValue; } }
        public ArgsTypeEnum ArgsType { get { return m_argsType; } }

        private ArgsTypeEnum m_argsType;
        private ulong m_rawInt;
        private double m_rawFloat;
        private object m_objValue;

        public HLArguments(uint ui)
        {
            m_rawInt = ui;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.U32;
            m_objValue = null;
        }

        public HLArguments(ulong ul)
        {
            m_rawInt = ul;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.U64;
            m_objValue = null;
        }

        public HLArguments(int i)
        {
            long signExtended = (long)i;
            m_rawInt = (ulong)signExtended;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.I32;
            m_objValue = null;
        }

        public HLArguments(long l)
        {
            m_rawInt = (ulong)l;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.I64;
            m_objValue = null;
        }

        public HLArguments(CLRTableRow row)
        {
            m_rawInt = 0;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.MetaToken;
            m_objValue = row;
        }

        public HLArguments(string str)
        {
            m_rawInt = 0;
            m_rawFloat = 0.0;
            m_argsType = ArgsTypeEnum.String;
            m_objValue = str;
        }

        public HLArguments(float f)
        {
            m_rawInt = 0;
            m_rawFloat = f;
            m_argsType = ArgsTypeEnum.F32;
            m_objValue = null;
        }

        public HLArguments(double f)
        {
            m_rawInt = 0;
            m_rawFloat = f;
            m_argsType = ArgsTypeEnum.F64;
            m_objValue = null;
        }

        public static HLArguments CreateNo(byte baseByte)
        {
            HLArguments args = new HLArguments();
            args.m_argsType = ArgsTypeEnum.No;
            args.m_rawInt = 0;
            args.m_rawFloat = 0.0;
            args.m_objValue = new No(baseByte);
            return args;
        }

        public static HLArguments CreateSwitch(int[] targets)
        {
            HLArguments args = new HLArguments();
            args.m_argsType = ArgsTypeEnum.Switch;
            args.m_rawInt = 0;
            args.m_rawFloat = 0.0;
            args.m_objValue = targets;
            return args;
        }

        public static HLArguments CreateUnpatchedSwitch(uint[] targets)
        {
            HLArguments args = new HLArguments();
            args.m_argsType = ArgsTypeEnum.Switch;
            args.m_rawInt = 0;
            args.m_rawFloat = 0.0;
            args.m_objValue = targets;
            return args;
        }
    }
}
