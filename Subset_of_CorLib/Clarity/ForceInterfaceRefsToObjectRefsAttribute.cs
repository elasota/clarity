using System;
using System.Collections.Generic;
using System.Text;

namespace Clarity
{
    // This attribute forces all interface reference parameters and return values
    // to reference the object instead of the interface implementation, similar
    // to how variance is implemented.
    //
    // The main purpose of this is implementing ICollection<T> and IList<T>
    // on arrays of interfaces.
    [AttributeUsage(AttributeTargets.Interface)]
    internal sealed class ForceInterfaceRefsToObjectRefsAttribute : Attribute
    {
    }
}
