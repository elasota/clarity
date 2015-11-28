#pragma once
#ifndef __CLARITY_INTERNAL_SUPPORT_H__
#define __CLARITY_INTERNAL_SUPPORT_H__

#include <float.h>
#include "ClarityCompilerDefs.h"
#include "ClarityTypes.h"

namespace ClarityInternal
{
	struct NoCreate
	{
	private:
		NoCreate();
	};

	template<class T>
	struct TypeDef
	{
		typedef T Type;
	private:
		TypeDef();
	};

	template<class T>
	struct TUnsignedOf : public NoCreate { };

	template<> struct TUnsignedOf<CLRTypes::S8> : public TypeDef<CLRTypes::U8> { };
	template<> struct TUnsignedOf<CLRTypes::S16> : public TypeDef<CLRTypes::U16> { };
	template<> struct TUnsignedOf<CLRTypes::S32> : public TypeDef<CLRTypes::U32> { };
	template<> struct TUnsignedOf<CLRTypes::S64> : public TypeDef<CLRTypes::U64> { };
	template<> struct TUnsignedOf<CLRTypes::U8> : public TypeDef<CLRTypes::U8> { };
	template<> struct TUnsignedOf<CLRTypes::U16> : public TypeDef<CLRTypes::U16> { };
	template<> struct TUnsignedOf<CLRTypes::U32> : public TypeDef<CLRTypes::U32> { };
	template<> struct TUnsignedOf<CLRTypes::U64> : public TypeDef<CLRTypes::U64> { };
}

namespace CLRPrivate
{
	template<class T, int TIsUnsigned>
	struct NumericLimits_ByTraits
	{
	};

	template<class T>
	struct NumericLimits_ByTraits<T, 1>
	{
		const T Minimum = static_cast<T>(static_cast<T>(-1) << (sizeof(T) * 8 - 1));
		const T Maximum = static_cast<T>(~Minimum);
		const T UnsignedMaximum = Maximum;
	};

	template<class T>
	struct NumericLimits_ByTraits<T, 0>
	{
		const T Minimum = static_cast<T>(0);
		const T Maximum = static_cast<T>(~static_cast<T>(0));
		const typename ClarityInternal::TUnsignedOf<T>::Value UnsignedMaximum = static_cast<typename ClarityInternal::TUnsignedOf<T>::Value>(Maximum);
	};

	template<>
	struct NumericLimits_ByTraits<float, 0>
	{
		const float Minimum = -FLT_MAX;
		const float Maximum = FLT_MAX;
	};

	template<>
	struct NumericLimits_ByTraits<double, 0>
	{
		const double Minimum = -DBL_MAX;
		const double Maximum = DBL_MAX;
	};
}

namespace ClarityInternal
{
	template<class T>
	struct IsUnsigned
	{
		enum
		{
			Value = (static_cast<T>(~static_cast<T>(0)) > 0) ? 1 : 0
		};
	};

	template<>
	struct IsUnsigned<float>
	{
		enum
		{
			Value = 0,
		};
	};

	template<>
	struct IsUnsigned<double>
	{
		enum
		{
			Value = 0,
		};
	};


	template<class T>
	struct NumericLimits
		: public CLRPrivate::NumericLimits_ByTraits<T, IsUnsigned<T>::Value>
	{
	};

	struct InternalNotImplementedException
	{
	};

    template<class T, int TExecute>
    struct ConditionalZeroFiller
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct ConditionalZeroFiller<T, 0>
        : public ::ClarityInternal::NoCreate
    {
        static void ZeroFill(T &instance);
    };

    template<class T>
    struct ConditionalZeroFiller<T, 1>
        : public ::ClarityInternal::NoCreate
    {
        static void ZeroFill(T &instance);
    };

	template<class TA, class TB>
	struct AreTypesSame
	{
		enum
		{
			Value = 0,
		};
	};

	template<class T>
	struct AreTypesSame<T, T>
	{
		enum
		{
			Value = 1,
		};
	};
}

#include <string.h>

template<class T>
CLARITY_FORCEINLINE void ::ClarityInternal::ConditionalZeroFiller<T, 1>::ZeroFill(T &instance)
{
	memset(&instance, 0, sizeof(T));
};

template<class T>
CLARITY_FORCEINLINE void ::ClarityInternal::ConditionalZeroFiller<T, 0>::ZeroFill(T &instance)
{
};

#endif
