#ifndef __CLARITY_UTIL_H__
#define __CLARITY_UTIL_H__

#include "ClarityCompilerDefs.h"
#include "ClarityInternalSupport.h"
#include "ClarityTypes.h"

namespace CLRCore
{
    struct RefTarget;
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
}

namespace CLRUtil
{
    template<class T>
    class StrictRef
    {
    public:
        StrictRef();
        StrictRef(const StrictRef &other);
        explicit StrictRef(T *ptr);
        StrictRef &operator =(const StrictRef& other);
        T *operator ->();
        const T *operator ->() const;

    private:
        T *m_ptr;
    };

    template<class T>
    struct TRef
        : public ::ClarityInternal::TypeDef< StrictRef<T> >
    {
    };


    template<class T>
    class AnchoredManagedPtr
    {
    public:
        void GetRefTargetAndValue(::CLRCore::RefTarget *&refTarget, T *&valuePtr);
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

    typedef ::CLRCore::RefTarget *TDGTarget;

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
    typename ::CLRVM::TRefValue<TDest>::Type PassiveConvertReference(typename ::CLRVM::TRefValue<TSource>::Type ref);

    template<class TSource, class TDest>
    typename ::CLRVM::TValValue<TDest>::Type PassiveConvertValue(typename ::CLRVM::TValValue<TSource>::Type ref);

    template<class T>
    T *ConvertDelegateTarget(::CLRUtil::TDGTarget dgtarget);

    template<class T>
    typename ::CLRVM::TRefValue<T>::Type NullReference();
}

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
CLARITY_FORCEINLINE T *::CLRUtil::StrictRef<T>::operator ->()
{
    return m_ptr;
}

template<class T>
CLARITY_FORCEINLINE const T *::CLRUtil::StrictRef<T>::operator ->() const
{
    return m_ptr;
}

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TValValue<TDest>::Type (::CLRUtil::PassiveConvertValue<TSource, TDest>)(typename ::CLRVM::TValValue<TSource>::Type val)
{
    return ::CLRUtil::PassiveValueConversionWriter<typename ::CLRVM::TValValue<TDest>::Type>::FromMid(::CLRUtil::PassiveValueConversionLoader<typename ::CLRVM::TValValue<TSource>::Type>::ToMid(val));
}

template<class T>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<T>::Type (::CLRVM::NullReference)()
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


#endif
