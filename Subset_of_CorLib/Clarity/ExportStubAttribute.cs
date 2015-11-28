using System;

namespace Clarity
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ExportStubAttribute : Attribute
    {
        public ExportStubAttribute(string sourcePath)
        {
        }
    }
}
