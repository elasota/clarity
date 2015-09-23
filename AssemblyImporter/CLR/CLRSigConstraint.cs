using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
