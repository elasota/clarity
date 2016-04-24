using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Clarity.Rpa
{
    public class HighRegionBuilder
    {
        private uint m_numCfgNodes;
        private Queue<HighCfgNode> m_unemittedNodes;
        private Dictionary<HighCfgNode, uint> m_cfgNodeLookup;
        private Dictionary<HighCfgNode, Dictionary<HighSsaRegister, uint>> m_cfgSsaLookups;

        public HighRegionBuilder()
        {
            m_numCfgNodes = 0;
            m_cfgNodeLookup = new Dictionary<HighCfgNode, uint>();
            m_cfgSsaLookups = new Dictionary<HighCfgNode, Dictionary<HighSsaRegister, uint>>();
            m_unemittedNodes = new Queue<HighCfgNode>();
        }

        public uint IndexCfgNode(HighCfgNode cfgNode)
        {
            uint index;
            if (m_cfgNodeLookup.TryGetValue(cfgNode, out index))
                return index;
            m_unemittedNodes.Enqueue(cfgNode);
            index = m_numCfgNodes++;
            m_cfgNodeLookup.Add(cfgNode, index);
            return index;
        }

        public HighCfgNode DequeueUnemittedNode()
        {
            if (m_unemittedNodes.Count == 0)
                return null;
            return m_unemittedNodes.Dequeue();
        }

        public void WriteHeader(BinaryWriter writer)
        {
            writer.Write(m_numCfgNodes);
        }

        private static Dictionary<HighSsaRegister, uint> CollectRegsDictFromNode(HighCfgNode cfgNode)
        {
            Dictionary<HighSsaRegister, uint> regDict = new Dictionary<HighSsaRegister, uint>();
            HighSsaRegister[] regList = CollectRegsFromNode(cfgNode);

            uint index = 0;
            foreach (HighSsaRegister reg in regList)
            {
                if (regDict.ContainsKey(reg))
                    throw new Exception("Duplicated SSA register");
                regDict.Add(reg, index++);
            }

            return regDict;
        }

        public static HighSsaRegister[] CollectRegsFromNode(HighCfgNode cfgNode)
        {
            List<HighSsaRegister> regList = new List<HighSsaRegister>();

            // This absolutely must match the emission order of HighCfgNode
            foreach (HighPhi phi in cfgNode.Phis)
                regList.Add(phi.Dest);

            HashSet<HighSsaRegister> gatheredConstants = new HashSet<HighSsaRegister>();

            HighInstruction.VisitSsaDelegate constAdder = delegate (ref HighSsaRegister use)
            {
                if (use.IsConstant && !gatheredConstants.Contains(use))
                {
                    gatheredConstants.Add(use);
                    regList.Add(use);
                }
            };

            foreach (HighInstruction instr in cfgNode.Instructions)
                instr.VisitSsaUses(constAdder);

            HighInstruction.VisitSsaDelegate destAdder = delegate (ref HighSsaRegister dest)
            {
                if (dest != null)
                    regList.Add(dest);
            };

            foreach (HighInstruction instr in cfgNode.Instructions)
                instr.VisitSsaDests(destAdder);

            return regList.ToArray();
        }

        public uint FindPredecessorSsaIndex(HighCfgNode predecessor, HighSsaRegister reg)
        {
            Dictionary<HighSsaRegister, uint> regsDict;
            if (!m_cfgSsaLookups.TryGetValue(predecessor, out regsDict))
            {
                regsDict = CollectRegsDictFromNode(predecessor);
                m_cfgSsaLookups.Add(predecessor, regsDict);
            }

            return regsDict[reg];
        }
    }
}
