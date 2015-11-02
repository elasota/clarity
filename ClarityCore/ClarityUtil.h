#ifndef __CLARITY_UTIL_H__
#define __CLARITY_UTIL_H__

#include "ClarityConfig.h"
#include "ClarityCompilerDefs.h"
#include "ClarityInternalSupport.h"
#include "ClarityTypes.h"

namespace CLRCore
{
    struct RefTarget;
}

namespace CLRExec
{
	struct IRefVisitor;
}

namespace CLRVM
{
    template<class T> struct TMaybeAnchoredManagedPtr;
    template<class T> struct TRefValue;
    template<class T> struct TValValue;
}

namespace CLRUtil
{
    template<class T> struct TRef;

	typedef ::CLRCore::RefTarget *TDGTarget;
}

namespace CLRPrivate
{
    template<int TIsValueType, class T>
    struct TValResolver
    {
    };

    template<class T>
    struct TValResolver < 1, T >
    {
        typedef T Type;
    };

    template<class T>
    struct TValResolver < 0, T >
    {
        typedef typename ::CLRUtil::TRef<T>::Type *Type;
    };

	template<int TIsInterface, class T>
	struct RefRetargeter
		: ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct RefRetargeter<0, T>
		: ::ClarityInternal::NoCreate
	{
		static T *Retarget(::CLRExec::IRefVisitor &visitor, T *ref);
	};

	template<class T>
	struct RefRetargeter<1, T>
		: public ::ClarityInternal::NoCreate
	{
		static T *Retarget(::CLRExec::IRefVisitor &visitor, T *ref);
	};

	template<int TIsValueType, int TIsInterface, class T>
	struct DelegateTargetConverter_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<1, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef ::CLRUtil::Boxed<T> *TResolvedTarget;

		static ::CLRUtil::Boxed<T> *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef T *TResolvedTarget;

		static T *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<0, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef T *TResolvedTarget;

		static T *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	template<class T>
	struct DelegateTargetConverter
		: public ::CLRPrivate::DelegateTargetConverter_ByTraits<::CLRTI::TypeProtoTraits<T>::IsValueType, ::CLRTI::TypeProtoTraits<T>::IsInterface, T>
	{
	};

	template<int TSourceIsInterface, int TDestIsInterface, class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<0, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<0, 1, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<1, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<1, 1, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};
}

namespace CLRUtil
{
#if CLARITY_USE_STRICT_REFS != 0
    template<class T>
    class StrictRef
    {
    public:
        StrictRef();
        StrictRef(const StrictRef &other);
        explicit StrictRef(T *ptr);
        StrictRef<T> &operator =(const StrictRef<T>& other);
		bool operator ==(const StrictRef<T>& other) const;
        T *operator ->();
        const T *operator ->() const;
		StrictRef<T> Retarget(::CLRExec::IRefVisitor &visitor) const;
		bool IsNull() const;
		T *GetPtr() const;

    private:
        T *m_ptr;
    };

    template<class T>
    struct TRef
        : public ::ClarityInternal::TypeDef< StrictRef<T> >
    {
    };

	template<class T>
	T *RefToPtr(const StrictRef<T> &ref);

#else
	template<class T>
	struct TRef
		: public ::ClarityInternal::TypeDef<T*>
	{
	};

	template<class T>
	T *RefToPtr(T *ref);
#endif

	template<class TSource, class TDest>
	struct PassiveReferenceConverter
		: public ::CLRPrivate::PassiveReferenceConverter_ByTraits<::CLRTI::TypeProtoTraits<TSource>::IsInterface, ::CLRTI::TypeProtoTraits<TDest>::IsInterface, TSource, TDest>
	{
	};

	template<class T>
	typename ::CLRUtil::TRef<T>::Type RetargetRef(::CLRExec::IRefVisitor &visitor, const typename ::CLRUtil::TRef<T>::Type & ref);

    template<class T>
    class AnchoredManagedPtr
    {
    public:
		void Visit(::CLRExec::IRefVisitor &refVisitor);

    private:
        ::CLRCore::RefTarget *m_object;
        T *m_value;
    };

    template<class T>
    struct TAnchoredManagedPtr
        : public ::ClarityInternal::TypeDef< typename ::CLRUtil::AnchoredManagedPtr<T> >
    {
    };

    template<class T>
    struct TDGBoundReturn
    {
        typedef T* Type;

    private:
        TDGBoundReturn();
    };

    template<class T>
    struct TVal
    {
        typedef typename ::CLRPrivate::TValResolver<::CLRTI::TypeProtoTraits<T>::IsValueType, T>::Type Type;
    };

    // Boxed<T>::Type is a container of a boxed value of type T
    template<class T>
    struct Boxed
    {
    private:
        Boxed();
    };

    template<class T>
    struct ConstrainedVtableGlue
    {
    private:
        ConstrainedVtableGlue();
    };

    template<class T>
    struct TRefParameter
        : public ::ClarityInternal::TypeDef<T&>
    {
    };

    // ValueThisParameter<T>::Type is the type passed as a "this" pointer to a method of a value type
    template<class T>
    struct TValueThisParameter
        : public ::ClarityInternal::TypeDef<typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type>
    {
    };


    ////////////////////////////////////////////////////////////////////////////////
    // Passive value loaders
    template<class TSource>
    struct PassiveValueConversionLoader { };

    template<class TSource, class TMid>
    struct PassiveValueSimpleConversionLoader
    {
        typedef TMid MidType;
        static TMid ToMid(TSource src);
    };

    template<>
    struct PassiveValueConversionLoader<::CLRTypes::Bool>
    {
        typedef ::CLRTypes::S32 MidType;
        static ::CLRTypes::S32 ToMid(::CLRTypes::Bool src);
    };

    template<> struct PassiveValueConversionLoader<::CLRTypes::U8>  : public PassiveValueSimpleConversionLoader<::CLRTypes::U8, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::U16> : public PassiveValueSimpleConversionLoader<::CLRTypes::U16, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::U32> : public PassiveValueSimpleConversionLoader<::CLRTypes::U32, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::U64> : public PassiveValueSimpleConversionLoader<::CLRTypes::U64, ::CLRTypes::S64> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::S8>  : public PassiveValueSimpleConversionLoader<::CLRTypes::S8, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::S16> : public PassiveValueSimpleConversionLoader<::CLRTypes::S16, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::S32> : public PassiveValueSimpleConversionLoader<::CLRTypes::S32, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionLoader<::CLRTypes::S64> : public PassiveValueSimpleConversionLoader<::CLRTypes::S64, ::CLRTypes::S64> { };

    ////////////////////////////////////////////////////////////////////////////////
    template<class TMid, class TDest>
    struct PassiveValueSimpleConversionWriter
    {
        static TDest FromMid(TMid src);
    };

    template<class TSource>
    struct PassiveValueConversionWriter { };

    template<>
    struct PassiveValueConversionWriter<::CLRTypes::Bool>
    {
        static ::CLRTypes::Bool FromMid(::CLRTypes::S32 src);
    };

    template<> struct PassiveValueConversionWriter<::CLRTypes::U8>  : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::U8> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::U16> : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::U16> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::U32> : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::U32> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::U64> : public PassiveValueSimpleConversionWriter<::CLRTypes::S64, ::CLRTypes::U64> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::S8>  : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::S8> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::S16> : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::S16> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::S32> : public PassiveValueSimpleConversionWriter<::CLRTypes::S32, ::CLRTypes::S32> { };
    template<> struct PassiveValueConversionWriter<::CLRTypes::S64> : public PassiveValueSimpleConversionWriter<::CLRTypes::S64, ::CLRTypes::S64> { };

    template<class T>
    typename ::CLRUtil::TAnchoredManagedPtr<T>::Type Unbox(::CLRUtil::Boxed<T> *box);

    template<class TSource, class TDest>
	struct PassiveValueConverter
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TValValue<TDest>::Type Convert(typename ::CLRVM::TValValue<TSource>::Type ref);
	};

    template<class T>
	typename ::CLRPrivate::DelegateTargetConverter<T>::TResolvedTarget ConvertDelegateTarget(::CLRUtil::TDGTarget dgtarget);

    template<class T>
    typename ::CLRVM::TRefValue<T>::Type NullReference();
}



template<class T>
CLARITY_FORCEINLINE T *::CLRPrivate::RefRetargeter<0, T>::Retarget(::CLRExec::IRefVisitor &visitor, T *ref)
{
	// Classes implement multiple ref targets.  Disambiguate to the root one.
	return static_cast<T*>(static_cast<::CLRCore::GCObject*>(visitor.TouchReference(static_cast<::CLRCore::GCObject*>(ref))));
}

template<class T>
CLARITY_FORCEINLINE T *::CLRPrivate::RefRetargeter<1, T>::Retarget(::CLRExec::IRefVisitor &visitor, T *ref)
{
	// Interfaces only implement one ref target.
	return static_cast<T*>(visitor.TouchReference(ref));
}


#if CLARITY_USE_STRICT_REFS != 0

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::StrictRef<T>::StrictRef()
{
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::StrictRef<T>::StrictRef(const StrictRef &other)
    : m_ptr(other.m_ptr)
{
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::StrictRef<T>::StrictRef(T *ptr)
    : m_ptr(ptr)
{
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::StrictRef<T> &::CLRUtil::StrictRef<T>::operator =(const ::CLRUtil::StrictRef<T>& other)
{
    this->m_ptr = other.m_ptr;
    return *this;
}

template<class T>
CLARITY_FORCEINLINE bool ::CLRUtil::StrictRef<T>::operator ==(const ::CLRUtil::StrictRef<T>& other) const
{
	return this->m_ptr == other.m_ptr;
}

template<class T>
CLARITY_FORCEINLINE T *::CLRUtil::StrictRef<T>::operator ->()
{
    return m_ptr;
}

template<class T>
CLARITY_FORCEINLINE const T *::CLRUtil::StrictRef<T>::operator ->() const
{
    return m_ptr;
}
template<class T>
CLARITY_FORCEINLINE ::CLRUtil::StrictRef<T> (::CLRUtil::StrictRef<T>::Retarget)(::CLRExec::IRefVisitor &visitor) const
{
	return ::CLRUtil::StrictRef<T>(::CLRPrivate::RefRetargeter<::CLRTI::TypeProtoTraits<T>::IsInterface, T>::Retarget(visitor, this->m_ptr));
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::Boxed<T> *::CLRPrivate::DelegateTargetConverter_ByTraits<1, 0, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast<::CLRUtil::Boxed<T>*>(static_cast<::CLRCore::GCObject*>(dgTarget));
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::TDGTarget (::CLRPrivate::DelegateTargetConverter_ByTraits<1, 0, T>::ToTarget)(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return static_cast<::CLRCore::GCObject*>(ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		);
}

template<class T>
CLARITY_FORCEINLINE T *::CLRPrivate::DelegateTargetConverter_ByTraits<0, 0, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast<T*>(static_cast<::CLRCore::GCObject*>(dgTarget));
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::TDGTarget (::CLRPrivate::DelegateTargetConverter_ByTraits<0, 0, T>::ToTarget)(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return static_cast<::CLRCore::GCObject*>(ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		);
}

template<class T>
CLARITY_FORCEINLINE T *::CLRPrivate::DelegateTargetConverter_ByTraits<0, 1, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast<T*>(dgTarget);
}

template<class T>
CLARITY_FORCEINLINE ::CLRUtil::TDGTarget (::CLRPrivate::DelegateTargetConverter_ByTraits<0, 1, T>::ToTarget)(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		;
}


template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<TDest>::Type (::CLRPrivate::PassiveReferenceConverter_ByTraits<0, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Object to object conversion
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr = srcPtr;
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<TDest>::Type (::CLRPrivate::PassiveReferenceConverter_ByTraits<0, 1, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Object to interface conversion
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr = srcPtr;
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
inline typename ::CLRVM::TRefValue<TDest>::Type (::CLRPrivate::PassiveReferenceConverter_ByTraits<1, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Interface to object conversion.  This is ONLY valid for conversion to System.Object
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr = (srcPtr == CLARITY_NULLPTR) ? CLARITY_NULLPTR : srcPtr->GetRootObject();
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
inline typename ::CLRVM::TRefValue<TDest>::Type (::CLRPrivate::PassiveReferenceConverter_ByTraits<1, 1, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Interface to interface conversion
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr;
	if (srcPtr == CLARITY_NULLPTR)
		destPtr = CLARITY_NULLPTR;
	else
		srcPtr->iPassiveConvertInterface(destPtr);
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class T>
CLARITY_FORCEINLINE bool (::CLRUtil::StrictRef<T>::IsNull)() const
{
	return this->m_ptr == CLARITY_NULLPTR;
}

template<class T>
CLARITY_FORCEINLINE T *(::CLRUtil::StrictRef<T>::GetPtr)() const
{
	return this->m_ptr;
}

template<class T>
typename ::CLRUtil::TRef<T>::Type (::CLRUtil::RetargetRef)(::CLRExec::IRefVisitor &visitor, const typename ::CLRUtil::TRef<T>::Type &ref)
{
	return ref.Retarget(visitor);
}

#else

template<class T>
::CLRUtil::TRef<T>::Type(::CLRUtil::RetargetRef)(::CLRExec::IRefVisitor &visitor, const ::CLRUtil::TRef<T>::Type & ref)
{
	return ::CLRPrivate::RefRetargeter<::CLRTI::TypeProtoTraits<T>::IsInterface, T>::Retarget(visitor, ref);
}

#endif

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TValValue<TDest>::Type (::CLRUtil::PassiveValueConverter<TSource, TDest>::Convert)(typename ::CLRVM::TValValue<TSource>::Type val)
{
    return ::CLRUtil::PassiveValueConversionWriter<typename ::CLRVM::TValValue<TDest>::Type>::FromMid(::CLRUtil::PassiveValueConversionLoader<typename ::CLRVM::TValValue<TSource>::Type>::ToMid(val));
}

template<class T>
CLARITY_FORCEINLINE typename ::CLRPrivate::DelegateTargetConverter<T>::TResolvedTarget (::CLRUtil::ConvertDelegateTarget)(::CLRUtil::TDGTarget dgtarget)
{
	return ::CLRPrivate::DelegateTargetConverter<T>::FromTarget(dgtarget);
}

template<class T>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<T>::Type (::CLRUtil::NullReference)()
{
    return ::CLRVM::TRefValue<T>::Type(static_cast<T*>(CLARITY_NULLPTR));
}


template<class TSource, class TMid>
CLARITY_FORCEINLINE TMid (::CLRUtil::PassiveValueSimpleConversionLoader<TSource, TMid>::ToMid)(TSource src)
{
    return TMid(src);
}

CLARITY_FORCEINLINE ::CLRTypes::S32 (::CLRUtil::PassiveValueConversionLoader<::CLRTypes::Bool>::ToMid)(::CLRTypes::Bool src)
{
    return (src == false) ? (::CLRTypes::S32(1)) : (::CLRTypes::S32(0));
};

template<class TMid, class TDest>
CLARITY_FORCEINLINE TDest (::CLRUtil::PassiveValueSimpleConversionWriter<TMid, TDest>::FromMid)(TMid mid)
{
    return TDest(mid);
};

CLARITY_FORCEINLINE ::CLRTypes::Bool (::CLRUtil::PassiveValueConversionWriter<::CLRTypes::Bool>::FromMid)(::CLRTypes::S32 mid)
{
    return ::CLRTypes::Bool(mid != 0);
}

template<class T>
inline void ::CLRUtil::AnchoredManagedPtr<T>::Visit(::CLRExec::IRefVisitor &refVisitor)
{
	::CLRCore::RefTarget *ref = this->m_object;
	if (ref != CLARITY_NULLPTR)
	{
		::CLRTypes::PtrDiffT valueOffset = reinterpret_cast<const ::CLRTypes::U8*>(this->m_value) - reinterpret_cast<const ::CLRTypes::U8*>(ref);
		ref = refVisitor.TouchReference(ref);
		this->m_object = ref;
		this->m_value = reinterpret_cast<T*>(reinterpret_cast<::CLRTypes::U8*>(ref) + valueOffset);
	}
}

#if CLARITY_USE_STRICT_REFS != 0

template<class T>
CLARITY_FORCEINLINE T *::CLRUtil::RefToPtr(const StrictRef<T> &ref)
{
	return ref.GetPtr();
}

#else

template<class T>
CLARITY_FORCEINLINE T *::CLRUtil::RefToPtr(T *ref)
{
	return ref;
}

#endif


#endif
