using System;

namespace AssemblyImporter
{
    public class ParseFailedException : Exception
    {
        public ParseFailedException(string msg) : base(msg)
        {
        }
    }
}
