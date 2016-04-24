
namespace Clarity.RpaCompiler
{
    public class CliMethodIndex
    {
        private uint m_depth;
        private uint m_index;

        public uint Depth { get { return m_depth; } }
        public uint Index { get { return m_index; } }

        public CliMethodIndex(uint depth, uint index)
        {
            m_depth = depth;
            m_index = index;
        }
    }
}
