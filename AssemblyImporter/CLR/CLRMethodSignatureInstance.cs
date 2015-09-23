using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyImporter.CLR
{
    public sealed class CLRMethodSignatureInstance : IEquatable<CLRMethodSignatureInstance>
    {
        public bool HasThis { get; private set; }
        public bool ExplicitThis { get; private set; }
        public uint NumGenericParameters { get; private set; }
        public CLRTypeSpec RetType { get; private set; }
        public CLRMethodSignatureInstanceParam[] ParamTypes { get; private set; }
        public CLRSigMethodDefOrRefSig.CallingConventionType CallingConvention { get; private set; }

        public CLRMethodSignatureInstance(CLRAssemblyCollection assemblies, CLRSigMethodDefOrRefSig sig)
        {
            RetType = assemblies.InternVagueType(sig.RetType.Type);
            List<CLRMethodSignatureInstanceParam> paramTypes = new List<CLRMethodSignatureInstanceParam>();
            foreach (CLRSigParamType paramType in sig.ParamTypes)
            {
                CLRTypeSpec paramTypeSpec = assemblies.InternVagueType(paramType.Type);
                CLRMethodSignatureInstanceParam param = new CLRMethodSignatureInstanceParam(paramType.TypeOfType, paramTypeSpec);
                paramTypes.Add(param);
            }

            ParamTypes = paramTypes.ToArray();
            CallingConvention = sig.CallingConvention;
            NumGenericParameters = sig.NumGenericParameters;
            ExplicitThis = sig.ExplicitThis;
            HasThis = sig.HasThis;
        }

        private CLRMethodSignatureInstance(CLRMethodSignatureInstance baseInstance, CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            HasThis = baseInstance.HasThis;
            ExplicitThis = baseInstance.ExplicitThis;
            NumGenericParameters = baseInstance.NumGenericParameters;
            RetType = baseInstance.RetType.Instantiate(typeParams, methodParams);
            List<CLRMethodSignatureInstanceParam> newParams = new List<CLRMethodSignatureInstanceParam>();
            foreach (CLRMethodSignatureInstanceParam oldParam in baseInstance.ParamTypes)
                newParams.Add(new CLRMethodSignatureInstanceParam(oldParam, typeParams, methodParams));
            ParamTypes = newParams.ToArray();
            CallingConvention = baseInstance.CallingConvention;
        }

        public CLRMethodSignatureInstance Instantiate(CLRTypeSpec[] typeParams, CLRTypeSpec[] methodParams)
        {
            return new CLRMethodSignatureInstance(this, typeParams, methodParams);
        }

        public bool UsesGenericTypeParams
        {
            get
            {
                if (RetType.UsesGenericTypeParams)
                    return true;
                foreach (CLRMethodSignatureInstanceParam param in ParamTypes)
                    if (param.Type.UsesGenericTypeParams)
                        return true;
                return false;
            }
        }

        public bool Equals(CLRMethodSignatureInstance other)
        {
            if (other.HasThis != this.HasThis)
                return false;
            if (other.ExplicitThis != this.ExplicitThis)
                return false;
            if (other.NumGenericParameters != this.NumGenericParameters)
                return false;
            if (!other.RetType.Equals(this.RetType))
                return false;
            if (other.ParamTypes.Length != this.ParamTypes.Length)
                return false;
            for (int i = 0; i < this.ParamTypes.Length; i++)
                if (!other.ParamTypes[i].Equals(this.ParamTypes[i]))
                    return false;
            if (other.CallingConvention != this.CallingConvention)
                return false;
            return true;
        }

        public override bool Equals(object other)
        {
            return other != null && other.GetType() == typeof(CLRMethodSignatureInstance) && this.Equals((CLRMethodSignatureInstance)other);
        }

        public override int GetHashCode()
        {
            int hashCode = 0;
            foreach (CLRMethodSignatureInstanceParam p in ParamTypes)
                hashCode += p.GetHashCode();
            return hashCode + HasThis.GetHashCode() + ExplicitThis.GetHashCode() + NumGenericParameters.GetHashCode() +
                RetType.GetHashCode() + ParamTypes.Length + CallingConvention.GetHashCode();
        }
    }
}
