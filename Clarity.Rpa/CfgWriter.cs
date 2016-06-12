using System;
using System.Collections.Generic;
using System.IO;

namespace Clarity.Rpa
{
    public class CfgWriter
    {
        private DisassemblyWriter m_dw;
        private UniqueQueue<HighCfgNode, object> m_cfgNodes = new UniqueQueue<HighCfgNode, object>();
        private Dictionary<HighLocal, uint> m_localIndexes = new Dictionary<HighLocal, uint>();
        private Dictionary<HighSsaRegister, uint> m_ssaIndexes = new Dictionary<HighSsaRegister, uint>();
        private Dictionary<HighCfgNode, uint> m_cfgNodeIndexes = new Dictionary<HighCfgNode, uint>();

        public CfgWriter(DisassemblyWriter dw, HighLocal[] locals)
        {
            m_dw = dw;
            for (int i = 0; i < locals.Length; i++)
                m_localIndexes.Add(locals[i], (uint)i);
        }

        private static T[] Flatten<T>(Dictionary<T, uint> dict)
        {
            T[] arr = new T[dict.Count];

            foreach (KeyValuePair<T, uint> kvp in dict)
                arr[kvp.Value] = kvp.Key;
            return arr;
        }

        private static uint FindIndexed<T>(Dictionary<T, uint> dict, T v)
        {
            uint result;
            if (dict.TryGetValue(v, out result))
                return result;
            result = (uint)(dict.Count + 1);
            dict.Add(v, result);
            return result;
        }

        public uint GetCfgNodeIndex(HighCfgNode node)
        {
            m_cfgNodes.Lookup(node);
            return FindIndexed<HighCfgNode>(m_cfgNodeIndexes, node);
        }

        public uint GetLocalIndex(HighLocal local)
        {
            return m_localIndexes[local];
        }

        public uint GetSsaIndex(HighSsaRegister ssa)
        {
            return FindIndexed<HighSsaRegister>(m_ssaIndexes, ssa);
        }

        public void WriteGraph()
        {
            m_dw.Mute();
            while (m_cfgNodes.HaveNext)
            {
                HighCfgNode cfgNode = m_cfgNodes.GetNext().Key;
                cfgNode.WriteDisassembly(this, m_dw);
            }
            m_dw.Unmute();

            // Write for real
            foreach (KeyValuePair<HighCfgNode, object> kvp in m_cfgNodes.AllInstances)
            {
                m_dw.Write("cfgNode bb");
                m_dw.Write(m_cfgNodeIndexes[kvp.Key].ToString());
                m_dw.WriteLine(" {");

                m_dw.PushIndent();
                kvp.Key.WriteDisassembly(this, m_dw);
                m_dw.PopIndent();
                m_dw.WriteLine("}");
            }
        }

        internal void WriteDefSsa(DisassemblyWriter dw, HighSsaRegister ssa)
        {
            if (ssa == null)
            {
                dw.Write("dead");
                return;
            }

            dw.Write("ssa");
            dw.Write(this.GetSsaIndex(ssa).ToString());
            dw.Write("(");
            dw.Write(ssa.ValueType.ToString());
            dw.Write(",");
            ssa.Type.WriteDisassembly(dw);
            dw.Write(")");
        }

        internal void WriteUseSsa(DisassemblyWriter dw, HighSsaRegister ssa)
        {
            if (ssa.IsConstant)
            {
                dw.Write("constant(");
                ssa.Type.WriteDisassembly(dw);
                dw.Write(",");
                if (ssa.ValueType == HighValueType.ConstantString)
                    dw.WriteQuoted((string)ssa.ConstantValue);
                else if (ssa.ValueType == HighValueType.Null)
                    dw.Write("null");
                else if (ssa.ValueType == HighValueType.ConstantValue)
                    dw.Write(ssa.ConstantValue.ToString());
                else
                    throw new ArgumentException();

                dw.Write(")");
            }
            else
            {
                dw.Write("ssa");
                dw.Write(GetSsaIndex(ssa).ToString());
            }
        }

        public void WriteEdge(DisassemblyWriter dw, HighCfgEdge edge)
        {
            if (edge == null)
                dw.Write("nowhere");
            else
            {
                dw.Write("bb");
                dw.Write(GetCfgNodeIndex(edge.Dest.Value).ToString());
            }
        }

        public void WriteCfgNode(DisassemblyWriter dw, HighCfgNode node)
        {
            dw.Write("bb");
            dw.Write(GetCfgNodeIndex(node).ToString());
        }

        public void WriteLocal(DisassemblyWriter dw, HighLocal local)
        {
            dw.Write("local");
            dw.Write(GetLocalIndex(local).ToString());
        }
    }
}
