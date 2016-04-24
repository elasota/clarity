namespace Clarity.Rpa
{
    public interface IBranchingInstruction
    {
        void VisitSuccessors(HighInstruction.VisitCfgEdgeDelegate visitor);
    }
}
