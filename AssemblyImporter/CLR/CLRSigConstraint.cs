using System;
using System.Collections.Generic;

namespace AssemblyImporter.CLR
{
    public class CLRSigConstraint
    {
        public enum ConstraintTypeEnum
        {
            Pinned,
        }

        public ConstraintTypeEnum ConstraintType { get; private set; }

        public CLRSigConstraint(ConstraintTypeEnum constraintType)
        {
            ConstraintType = constraintType;
        }
    }
}
