using System.Collections.Generic;

namespace Clarity.Rpa
{
    public class HighMethodBuilder
    {
        private Dictionary<HighLocal, uint> m_localIndexes;

        public HighMethodBuilder(IEnumerable<HighLocal> locals)
        {
            m_localIndexes = new Dictionary<HighLocal, uint>();

            uint numLocals = 0;
            foreach (HighLocal local in locals)
                m_localIndexes.Add(local, numLocals++);
        }

        public uint LookupLocal(HighLocal local)
        {
            return m_localIndexes[local];
        }
    }
}
