namespace Clarity.Rpa
{
    public enum HighValueType
    {
        ManagedPtr,
        ValueValue,
        ReferenceValue,

        Null,
        ConstantValue,
        ConstantString,

        // Compiler-only types
        BoxedValue
    }
}
