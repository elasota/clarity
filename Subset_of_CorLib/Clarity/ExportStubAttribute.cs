using System;

namespace Clarity
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal sealed class ExportStubAttribute : Attribute
    {
        public ExportStubAttribute(string sourcePath)
        {
        }
    }
}
