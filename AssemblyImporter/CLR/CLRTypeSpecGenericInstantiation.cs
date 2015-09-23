using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public sealed class CLRTypeSpecGenericInstantiation : CLRTypeSpec, IEquatable<CLRTypeSpecGenericInstantiation>
    {
        public CLRSigTypeGenericInstantiation.InstType InstantiationType { get; private set; }
        public CLRTypeSpecClass GenericType { get; private set; }
        public CLRTypeSpec[] ArgTypes { get; private set; }

        public CLRTypeSpecGenericInstantiation(CLRSigTypeGenericInstantiation.InstType instType, CLRTypeSpecClass genericType, CLRTypeSpec[] argTypes)
        {
            InstantiationType = instType;
            GenericType = genericType;
            ArgTypes = argTypes;
        }
        
        private CLRTypeSpecGenericInstantiation(CLRTypeSpecGenericInstantiation baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            InstantiationType = baseInstance.InstantiationType;
            GenericType = baseInstance.GenericType;
            List<CLRTypeSpec> newArgs = new List<CLRTypeSpec>();
            foreach (CLRTypeSpec arg in baseInstance.ArgTypes)
                newArgs.Add(arg.Instantiate(typeParams, methodParams));
            ArgTypes = newArgs.ToArray();
        }

        public override int GetHashCode()
        {
            int hashCode = InstantiationType.GetHashCode();
            hashCode += GenericType.GetHashCode();
            hashCode += ArgTypes.Length;
            foreach (CLRTypeSpec argType in ArgTypes)
                hashCode += argType.GetHashCode();
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(CLRTypeSpecGenericInstantiation) && this.Equals((CLRTypeSpecGenericInstantiation)obj);
        }

        public bool Equals(CLRTypeSpecGenericInstantiation other)
        {
            if (other.InstantiationType != InstantiationType)
                return false;
            if (!other.GenericType.Equals(GenericType))
                return false;
            if (other.ArgTypes.Length != ArgTypes.Length)
                return false;
            for (int i = 0; i < ArgTypes.Length; i++)
                if (!other.ArgTypes[i].Equals(ArgTypes[i]))
                    return false;
            return true;
        }

        public override CLRTypeSpec Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CLRTypeSpecGenericInstantiation(this, typeParams, methodParams);
        }

        public override bool UsesGenericParamOfType(CLRSigType.ElementType elementType)
        {
            foreach (CLRTypeSpec argType in ArgTypes)
                if (argType.UsesGenericParamOfType(elementType))
                    return true;
            return false;
        }
    }
}
