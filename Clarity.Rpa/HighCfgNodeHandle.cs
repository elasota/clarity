namespace Clarity.Rpa
{
    public class HighCfgNodeHandle : SimpleHandle<HighCfgNode>
    {
        public HighCfgNodeHandle()
            : base(null)
        {
        }

        public HighCfgNodeHandle(HighCfgNode cfgNode)
            : base(cfgNode)
        {
        }
    }
}
