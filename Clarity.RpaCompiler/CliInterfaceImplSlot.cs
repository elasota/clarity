namespace Clarity.RpaCompiler
{
    public struct CliInterfaceImplSlot
    {
        private bool m_haveNewImpl;
        private uint m_classVTableSlot;

        public bool HaveNewImpl { get { return m_haveNewImpl; } }
        public uint ClassVTableSlot { get { return m_classVTableSlot; } }

        public CliInterfaceImplSlot(bool haveNewImpl, uint classVTableSlot)
        {
            m_haveNewImpl = haveNewImpl;
            m_classVTableSlot = classVTableSlot;
        }
    }
}
