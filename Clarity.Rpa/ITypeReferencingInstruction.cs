namespace Clarity.Rpa
{
    public interface ITypeReferencingInstruction
    {
        void VisitTypes(HighInstruction.VisitTypeSpecDelegate visitor);
    }
}
