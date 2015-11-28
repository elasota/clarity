#pragma once
#ifndef __CLARITY_NUMBER_CONVERSIONS_H__
#define __CLARITY_NUMBER_CONVERSIONS_H__

#include "ClarityTypes.h"
#include "ClarityCompilerDefs.h"
#include "ClarityInternalSupport.h"

namespace CLRExec
{
	class Frame;
}

namespace CLRPrivate
{
	struct NumberConverter_ExpandSource
	{
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::S8 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::S16 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::S32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S64 ExpandSource(CLRTypes::S64 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::U8 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::U16 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 ExpandSource(CLRTypes::U32 v) { return static_cast<CLRTypes::S32>(static_cast<CLRTypes::U32>(v)); }
		CLARITY_FORCEINLINE static CLRTypes::S64 ExpandSource(CLRTypes::U64 v) { return static_cast<CLRTypes::S64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::F32 ExpandSource(CLRTypes::F32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::F64 ExpandSource(CLRTypes::F64 v) { return v; }
	};

	template<class TSource, int TUnsignedExpansion>
	struct TNumberConverter_ExpandedIntType
		: public ClarityInternal::NoCreate
	{
	};

	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U8, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U16, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U32, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U64, 0> : public ClarityInternal::TypeDef<CLRTypes::U64> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S8, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S16, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S32, 0> : public ClarityInternal::TypeDef<CLRTypes::U32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S64, 0> : public ClarityInternal::TypeDef<CLRTypes::U64> { };

	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U8, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U16, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U32, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::U64, 1> : public ClarityInternal::TypeDef<CLRTypes::S64> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S8, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S16, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S32, 1> : public ClarityInternal::TypeDef<CLRTypes::S32> { };
	template<> struct TNumberConverter_ExpandedIntType<CLRTypes::S64, 1> : public ClarityInternal::TypeDef<CLRTypes::S64> { };


	template<class TSource, int TSourceIsUnsigned, class TDest, int TDestIsUnsigned>
	struct NumberConverter_ValidityChecker_ByTraits
	{
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker_ByTraits<TSource, 0, TDest, 0>
	{
		inline static bool IsRepresentable(TSource v)
		{
			// Signed source, signed dest
			return v >= ClarityInternal::NumericLimits<TDest>::Minimum && v <= ClarityInternal::NumericLimits<TDest>::Maximum;
		}
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker_ByTraits<TSource, 1, TDest, 0>
	{
		inline static bool IsRepresentable(TSource v)
		{
			// Unsigned source, signed dest
			return v <= ClarityInternal::NumericLimits<TDest>::UnsignedMaximum;
		}
	};

	template<class TSource, class TDest, int TSourceIsLarger>
	struct NumberConverter_ValidityChecker_ByTraits_SignedToUnsigned
	{
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker_ByTraits_SignedToUnsigned<TSource, TDest, 0>
	{
		inline static bool IsRepresentable(TSource v)
		{
			// Signed to unsigned, source is not larger
			return v >= 0;
		}
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker_ByTraits_SignedToUnsigned<TSource, TDest, 1>
	{
		inline static bool IsRepresentable(TSource v)
		{
			// Signed to unsigned, source is larger
			return v >= 0 && v <= ClarityInternal::NumericLimits<TDest>::UnsignedMaximum;
		}
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker_ByTraits<TSource, 0, TDest, 1>
		: public NumberConverter_ValidityChecker_ByTraits_SignedToUnsigned<TSource, TDest, (sizeof(TSource) > sizeof(TDest)) ? 1 : 0 >
	{
	};

	template<class TSource, class TDest>
	struct NumberConverter_ValidityChecker
		: public NumberConverter_ValidityChecker_ByTraits <
			TSource,
			ClarityInternal::IsUnsigned<TSource>::Value,
			TDest,
			ClarityInternal::IsUnsigned<TDest>::Value
		>
	{
	};

	template<class TDest>
	struct NumberConverter_ConvertToTarget
	{
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::S8>
	{
		CLARITY_FORCEINLINE static CLRTypes::S8 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::S8>(v & 0xff); }
		CLARITY_FORCEINLINE static CLRTypes::S8 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::S8>(v & 0xff); }
		CLARITY_FORCEINLINE static CLRTypes::S8 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::S8>(v); }
		CLARITY_FORCEINLINE static CLRTypes::S8 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::S8>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::S16>
	{
		CLARITY_FORCEINLINE static CLRTypes::S16 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::S16>(v & 0xffff); }
		CLARITY_FORCEINLINE static CLRTypes::S16 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::S16>(v & 0xffff); }
		CLARITY_FORCEINLINE static CLRTypes::S16 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::S16>(v); }
		CLARITY_FORCEINLINE static CLRTypes::S16 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::S16>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::S32>
	{
		CLARITY_FORCEINLINE static CLRTypes::S32 Convert(CLRTypes::S32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S32 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::S32>(v & 0xffffffff); }
		CLARITY_FORCEINLINE static CLRTypes::S32 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::S32>(v); }
		CLARITY_FORCEINLINE static CLRTypes::S32 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::S32>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::S64>
	{
		CLARITY_FORCEINLINE static CLRTypes::S64 Convert(CLRTypes::S32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S64 Convert(CLRTypes::S64 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::S64 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::S64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::S64 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::S64>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::U8>
	{
		CLARITY_FORCEINLINE static CLRTypes::U8 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::U8>(v & 0xff); }
		CLARITY_FORCEINLINE static CLRTypes::U8 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::U8>(v & 0xff); }
		CLARITY_FORCEINLINE static CLRTypes::U8 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::U8>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U8 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::U8>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::U16>
	{
		CLARITY_FORCEINLINE static CLRTypes::U16 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::U16>(v & 0xffff); }
		CLARITY_FORCEINLINE static CLRTypes::U16 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::U16>(v & 0xffff); }
		CLARITY_FORCEINLINE static CLRTypes::U16 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::U16>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U16 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::U16>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::U32>
	{
		CLARITY_FORCEINLINE static CLRTypes::U32 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::U32>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U32 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::U32>(v & 0xffffffff); }
		CLARITY_FORCEINLINE static CLRTypes::U32 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::U32>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U32 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::U32>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::U64>
	{
		CLARITY_FORCEINLINE static CLRTypes::U64 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::U64>(v & 0xffffffff); }
		CLARITY_FORCEINLINE static CLRTypes::U64 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::U64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U64 Convert(CLRTypes::F32 v) { return static_cast<CLRTypes::U64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::U64 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::U64>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::F32>
	{
		CLARITY_FORCEINLINE static CLRTypes::F32 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::F32>(v); }
		CLARITY_FORCEINLINE static CLRTypes::F32 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::F32>(v); }
		CLARITY_FORCEINLINE static CLRTypes::F32 Convert(CLRTypes::F32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::F32 Convert(CLRTypes::F64 v) { return static_cast<CLRTypes::F32>(v); }
	};

	template<>
	struct NumberConverter_ConvertToTarget<CLRTypes::F64>
	{
		CLARITY_FORCEINLINE static CLRTypes::F64 Convert(CLRTypes::S32 v) { return static_cast<CLRTypes::F64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::F64 Convert(CLRTypes::S64 v) { return static_cast<CLRTypes::F64>(v); }
		CLARITY_FORCEINLINE static CLRTypes::F64 Convert(CLRTypes::F32 v) { return v; }
		CLARITY_FORCEINLINE static CLRTypes::F64 Convert(CLRTypes::F64 v) { return v; }
	};
}

namespace CLRVM
{
	template<class TSource, class TDest, int TIsOvf, int TIsUn>
	struct NumberConverter
		: public ClarityInternal::NoCreate
	{
	};

	template<class TSource, class TDest>
	struct NumberConverter<TSource, TDest, 0, 0>
	{
		CLARITY_FORCEINLINE static TDest Convert(TSource v)
		{
			return CLRPrivate::NumberConverter_ConvertToTarget<TDest>::Convert(CLRPrivate::NumberConverter_ExpandSource::ExpandSource(v));
		}
	};

	// .ovf (signed)
	template<class TSource, class TDest>
	struct NumberConverter<TSource, TDest, 1, 0>
	{
		inline static TDest Convert(const CLRExec::Frame &frame, TSource v)
		{
			typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 0>::Type expandedV = CLRPrivate::NumberConverter_ExpandSource::ExpandSource(v);

			if (!CLRPrivate::NumberConverter_ValidityChecker<typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 0>::Type, TDest>::IsRepresentable(expandedV))
			{
				CLARITY_NOTIMPLEMENTED;
			}
			return CLRPrivate::NumberConverter_ConvertToTarget<TDest>::Convert(expandedV);
		}
	};

	// .ovf unsigned
	template<class TSource, class TDest>
	struct NumberConverter<TSource, TDest, 1, 1>
	{
		inline static TDest CheckedConvert(const CLRExec::Frame &frame, TSource v)
		{
			typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 1>::Type expandedV = 
				static_cast<typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 1>::Type>(CLRPrivate::NumberConverter_ExpandSource::ExpandSource(v));

			if (!CLRPrivate::NumberConverter_ValidityChecker<typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 1>::Type, TDest>::IsRepresentable(expandedV))
			{
				CLARITY_NOTIMPLEMENTED;
			}
			return CLRPrivate::NumberConverter_ConvertToTarget<TDest>::Convert(expandedV);
		}
	};

	// unsigned, but no ovf
	// This is only used for unsigned int to float conversions
	template<class TSource>
	struct NumberConverter<TSource, CLRTypes::F64, 0, 1>
	{
		CLARITY_FORCEINLINE static CLRTypes::F64 CheckedConvert(const CLRExec::Frame &frame, TSource v)
		{
			typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 1>::Type expandedV =
				static_cast<typename CLRPrivate::TNumberConverter_ExpandedIntType<TSource, 1>::Type>(CLRPrivate::NumberConverter_ExpandSource::ExpandSource(v));

			return static_cast<CLRTypes::F64>(expandedV);
		}
	};

	template<class TSource, class TDest, int TIsOvf, int TIsUn>
	struct ClrSemanticNumberConverter
	{
		CLARITY_FORCEINLINE static typename ::CLRVM::TValValue<TDest>::Type CheckedConvert(const CLRExec::Frame &frame, typename ::CLRVM::TValValue<TSource>::Type v)
		{
			return typename ::CLRVM::TValValue<TDest>::Type(
				NumberConverter<typename CLRVM::TNumberStorageType<TSource>::Type, typename CLRVM::TNumberStorageType<TDest>::Type, TIsOvf, TIsUn>::Convert(
						frame,
						::CLRTypes::NumberDetagger<typename ::CLRVM::TValValue<TSource>::Type>::Detag(v)
					)
				);
		}

		CLARITY_FORCEINLINE static typename ::CLRVM::TValValue<TDest>::Type Convert(typename ::CLRVM::TValValue<TSource>::Type v)
		{
			return typename ::CLRVM::TValValue<TDest>::Type(
					NumberConverter<typename CLRVM::TNumberStorageType<TSource>::Type, typename CLRVM::TNumberStorageType<TDest>::Type, TIsOvf, TIsUn>::Convert(
						::CLRTypes::NumberDetagger<typename ::CLRVM::TValValue<TSource>::Type>::Detag(v)
						)
				);
		}
	};
}


#endif

