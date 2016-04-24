namespace Clarity.Rpa
{
    public interface IMethodReferencingInstruction
    {
        void VisitMethodSpecs(HighInstruction.VisitMethodSpecDelegate visitor);
    }
}
