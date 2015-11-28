using System;

namespace Clarity
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ExportStubAttribute : Attribute
    {
        public ExportStubAttribute(string sourcePath)
        {
        }
    }
}
