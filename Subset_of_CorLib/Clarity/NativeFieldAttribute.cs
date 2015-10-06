using System;

namespace Clarity
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class NativeFieldAttribute : Attribute
    {
        public NativeFieldAttribute(string definitionPath, string fieldType, string fieldName)
        {
        }
    }
}
