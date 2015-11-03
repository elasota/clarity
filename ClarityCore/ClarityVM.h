#pragma once
#ifndef __CLARITY_VM_H__
#define __CLARITY_VM_H__

#include "ClarityConfig.h"
#include "ClarityInternalSupport.h"
#include "ClarityTypes.h"

namespace CLRX
{
    namespace NtSystem
    {
        struct tBoolean;
        struct tByte;
        struct tSByte;
        struct tInt16;
        struct tUInt16;
        struct tInt32;
        struct tUInt32;
        struct tInt64;
        struct tUInt64;
    }
}

namespace CLRUtil
{
    template<class T> struct TLocalManagedPtr;
    template<class T> struct TAnchoredManagedPtr;
    template<class T> struct TRef;
}

namespace CLRVM
{
    template<class T> struct TransientLocal;
    template<class T> struct NoInitPermanentLocal;
    template<class T> struct InitPermanentLocal;
}

namespace CLRCore
{
    struct RefTarget;
}

namespace CLRExec
{
    class Frame;
}

namespace CLRPrivate
{
    template<class TValueType>
    struct VRegOps_ByValueType
    {
        static TValueType &Liven(::CLRVM::NoInitPermanentLocal<TValueType> &local);
        static TValueType &Liven(::CLRVM::TransientLocal<TValueType> &local);
        static void Kill(::CLRVM::NoInitPermanentLocal<TValueType> &local);
        static void Kill(::CLRVM::TransientLocal<TValueType> &local);
        static TValueType &KillAndReturn(::CLRVM::NoInitPermanentLocal<TValueType> &local);
        static TValueType &KillAndReturn(::CLRVM::TransientLocal<TValueType> &local);
    };

	template<class T, int TIsValueType>
	struct TValueObjectTypeResolver_IsValueType
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TValueObjectTypeResolver_IsValueType<T, 1>
		: public ::ClarityInternal::TypeDef< ::CLRUtil::Boxed<T> >
	{
	};

	template<class T>
	struct TValueObjectTypeResolver_IsValueType<T, 0>
		: public ::ClarityInternal::TypeDef< T >
	{
	};

    template<class T, int TIsValueType>
    struct TValValueResolver_IsValueType
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TValValueResolver_IsValueType<T, 1>
        : public ::ClarityInternal::TypeDef< T >
    {
    };

    template<class T>
    struct TValValueResolver_IsValueType<T, 0>
        : public ::ClarityInternal::TypeDef< typename ::CLRUtil::TRef<T>::Type >
    {
    };

	template<int TIsValueType, int TIsValueTraceable, class T>
	struct ValTracer
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct ValTracer<0, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(::CLRExec::IRefVisitor &visitor, typename ::CLRUtil::TRef<T>::Type &ref);
	};

	template<class T>
	struct ValTracer<1, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(::CLRExec::IRefVisitor &visitor, T &ref);
	};

	template<class T>
	struct ValTracer<1, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(::CLRExec::IRefVisitor &visitor, T &ref);
	};

	template<int TAIsInterface, int TBIsInterface, int TTypesAreSame, class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<int TTypesAreSame, class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<0, 0, TTypesAreSame, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<1, 0, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<0, 1, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<1, 1, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class T>
	struct ReferenceEqualityComparer_ByTraits<1, 1, 1, T, T>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<T>::Type &a, const typename ::CLRVM::TRefValue<T>::Type &b);
	};
}

namespace CLRVM
{
    template<class T>
    struct TransientLocal
    {
#if CLARITY_PRECISE_TEMPORARY_MARKING
        bool m_isAlive;
#endif
        typedef T LocalValueType;

        T m_value;

        TransientLocal();
    };

    template<class T>
    struct NoInitPermanentLocal
    {
        typedef T LocalValueType;

        T m_value;
    };

    template<class T>
    struct InitPermanentLocal
    {
        typedef T LocalValueType;

        T m_value;

        InitPermanentLocal();
    };

    template<class T>
    struct TMaybeAnchoredManagedPtr
#if CLARITY_MEMORY_RELOCATION != 0
        : public ::ClarityInternal::TypeDef<typename ::CLRUtil::TAnchoredManagedPtr<T>::Type>

#else
        : public ::ClarityInternal::TypeDef<T*>
#endif
    {
    };

	template<class T>
	struct TValueObjectType
		: public ::ClarityInternal::TypeDef<typename ::CLRPrivate::TValueObjectTypeResolver_IsValueType<T, ::CLRTI::TypeProtoTraits<T>::IsValueType>::Type>
	{
	};

    template<class T>
    struct TRefValue
        : public ::ClarityInternal::TypeDef< typename ::CLRUtil::TRef< typename ::CLRVM::TValueObjectType<T>::Type >::Type >
    {
    };

    template<class T>
    struct TValValue
        : public ::ClarityInternal::TypeDef<typename ::CLRPrivate::TValValueResolver_IsValueType<T, ::CLRTI::TypeProtoTraits<T>::IsValueType>::Type>
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tByte >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::U8 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tSByte >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::S8 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tInt16 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::S16 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tUInt16 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::U16 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tInt32 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::S32 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tUInt32 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::U32 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tInt64 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::S64 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tUInt64 >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::U64 >
    {
    };

    template<>
    struct TValValue< ::CLRX::NtSystem::tBoolean >
        : public ::ClarityInternal::TypeDef< ::CLRTypes::Bool >
    {
    };

    template<class T>
    typename ::CLRUtil::TRef<T>::Type ParamThis(T *bThis);

    template<class T>
    typename ::CLRVM::TRefValue<T>::Type AllocObject(const ::CLRExec::Frame &frame);

    struct ELocalType
        : public ::ClarityInternal::NoCreate
    {
        enum Enum
        {
            Argument,
            Local,
            Temporary
        };
    };

    // Ref local
    template<int TLocalType, class T>
    struct TRefLocal
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TRefLocal<ELocalType::Argument, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TRefValue<T>::Type > >
    {
    };

    template<class T>
    struct TRefLocal<ELocalType::Local, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TRefValue<T>::Type > >
    {
    };

    template<class T>
    struct TRefLocal<ELocalType::Temporary, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TRefValue<T>::Type > >
    {
    };

    // Anchored managed ptr local
    template<int TLocalType, class T>
    struct TAnchoredManagedPtrLocal
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TAnchoredManagedPtrLocal<ELocalType::Argument, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TAnchoredManagedPtrLocal<ELocalType::Local, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TAnchoredManagedPtrLocal<ELocalType::Temporary, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
    {
    };

    // Local managed ptr local
    template<int TLocalType, class T>
    struct TLocalManagedPtrLocal
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TLocalManagedPtrLocal<ELocalType::Argument, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TLocalManagedPtrLocal<ELocalType::Local, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TLocalManagedPtrLocal<ELocalType::Temporary, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
    {
    };

    // Maybe anchored managed ptr local
    template<int TLocalType, class T>
    struct TMaybeAnchoredManagedPtrLocal
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TMaybeAnchoredManagedPtrLocal<ELocalType::Argument, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TMaybeAnchoredManagedPtrLocal<ELocalType::Local, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
    {
    };

    template<class T>
    struct TMaybeAnchoredManagedPtrLocal<ELocalType::Temporary, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
    {
    };

    // Value local
    template<int TLocalType, class T>
    struct TValLocal
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TValLocal<ELocalType::Argument, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TValValue<T>::Type > >
    {
    };

    template<class T>
    struct TValLocal<ELocalType::Local, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TValValue<T>::Type > >
    {
    };

    template<class T>
    struct TValLocal<ELocalType::Temporary, T>
        : public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TValValue<T>::Type > >
    {
    };

    // Local tracers
    template<int TLocalType, class T>
    struct LocalTracerFuncs
    {
        static void TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<TLocalType, T>::Type &ref);
        static void TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<TLocalType, T>::Type &ref);
        static void TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<TLocalType, T>::Type &ref);
        static void TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<TLocalType, T>::Type &ref);

    private:
        LocalTracerFuncs();
    };

    // Value tracers
    template<class T>
    struct TracerFuncs
    {
        static void TraceAnchoredManagedPtr(::CLRExec::IRefVisitor &visitor, typename ::CLRUtil::TAnchoredManagedPtr<T>::Type &ref);
        static void TraceMaybeAnchoredManagedPtr(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type &ref);
        static void TraceVal(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TValValue<T>::Type &ref);
        static void TraceRef(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TRefValue<T>::Type &ref);
    };

#if CLARITY_PRECISE_TEMPORARY_MARKING
    // Local tracers
    template<class T>
    struct LocalTracerFuncs<ELocalType::Temporary, T>
    {
        static void TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<ELocalType::Temporary, T>::Type &ref);
        static void TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<ELocalType::Temporary, T>::Type &ref);
        static void TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<ELocalType::Temporary, T>::Type &ref);
        static void TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<ELocalType::Temporary, T>::Type &ref);

    private:
        LocalTracerFuncs();
    };
#endif

    template<class T>
    typename ::CLRVM::TRefValue<T>::Type AllocObject(const ::CLRExec::Frame &frame);

    template<class TLocalType>
    typename TLocalType::LocalValueType &LivenVReg(TLocalType &local);

    template<class TLocalType>
    typename TLocalType::LocalValueType &KillAndReturnVReg(TLocalType &local);

    template<class TLocalType>
    void KillVReg(TLocalType &local);

    template<class TLocalType>
    typename TLocalType::LocalValueType &VRegValue(TLocalType &local);

    template<class TA, class TB>
	struct ReferenceEqualityComparer
		: public ::CLRPrivate::ReferenceEqualityComparer_ByTraits<
			::CLRTI::TypeProtoTraits<TA>::IsInterface,
			::CLRTI::TypeProtoTraits<TB>::IsInterface,
			::ClarityInternal::AreTypesSame<TA, TB>::Value,
			TA, TB
		>
	{
	};

	template<class T>
	bool IsZero(const typename ::CLRVM::TValValue<T>::Type &a);

	template<class T>
	bool IsNull(const typename ::CLRVM::TRefValue<T>::Type &a);
}

///////////////////////////////////////////////////////////////////////////////
#include "ClarityExec.h"


template<class T>
CLARITY_FORCEINLINE void ::CLRPrivate::ValTracer<0, 1, T>::Trace(::CLRExec::IRefVisitor &visitor, typename ::CLRUtil::TRef<T>::Type &ref)
{
	ref = ::CLRUtil::RetargetRef<T>(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void ::CLRPrivate::ValTracer<1, 0, T>::Trace(::CLRExec::IRefVisitor &visitor, T &ref)
{
}

template<class T>
CLARITY_FORCEINLINE void ::CLRPrivate::ValTracer<1, 1, T>::Trace(::CLRExec::IRefVisitor &visitor, T &ref)
{
	ref.VisitReferences(visitor);
}

#if CLARITY_PRECISE_TEMPORARY_MARKING

template<class T>
CLARITY_FORCEINLINE::CLRVM::TransientLocal<T>::TransientLocal()
    : m_isAlive(false)
{
}

#else

template<class T>
CLARITY_FORCEINLINE::CLRVM::TransientLocal<T>::TransientLocal()
{
    memset(&this->m_value, 0, sizeof(this->m_value));
};

#endif

template<class T>
CLARITY_FORCEINLINE::CLRVM::InitPermanentLocal<T>::InitPermanentLocal()
{
    memset(&this->m_value, 0, sizeof(this->m_value));
};

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &(::CLRVM::LivenVReg)(TLocalType &local)
{
    return ::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::Liven(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &(::CLRVM::KillAndReturnVReg)(TLocalType &local)
{
    return ::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::KillAndReturn(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE void ::CLRVM::KillVReg(TLocalType &local)
{
    ::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::Kill(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &::CLRVM::VRegValue(TLocalType &local)
{
    return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::Liven(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
    return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::Liven(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
    local.m_isAlive = true;
#endif
    return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE void ::CLRPrivate::VRegOps_ByValueType<TValueType>::Kill(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
}

template<class TValueType>
CLARITY_FORCEINLINE void ::CLRPrivate::VRegOps_ByValueType<TValueType>::Kill(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
    local.m_isAlive = false;
#endif
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::KillAndReturn(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
    return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::KillAndReturn(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
    local.m_isAlive = false;
#endif
    return local.m_value;
}


template<class T>
CLARITY_FORCEINLINE typename ::CLRUtil::TRef<T>::Type (::CLRVM::ParamThis)(T *bThis)
{
    return typename ::CLRUtil::TRef<T>::Type(bThis);
}

// Local tracers
template<int TLocalType, class T>
CLARITY_FORCEINLINE void ::CLRVM::LocalTracerFuncs<TLocalType, T>::TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<TLocalType, T>::Type &ref)
{
    ::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void ::CLRVM::LocalTracerFuncs<TLocalType, T>::TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<TLocalType, T>::Type &ref)
{
    ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void ::CLRVM::LocalTracerFuncs<TLocalType, T>::TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<TLocalType, T>::Type &ref)
{
    ::CLRVM::TracerFuncs<T>::TraceVal(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void ::CLRVM::LocalTracerFuncs<TLocalType, T>::TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<TLocalType, T>::Type &ref)
{
    ::CLRVM::TracerFuncs<T>::TraceRef(visitor, ref.m_value);
}


template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(::CLRExec::IRefVisitor &visitor, typename ::CLRUtil::TAnchoredManagedPtr<T>::Type &ref)
{
	ref.Visit(visitor);
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type &ref)
{
#if CLARITY_MEMORY_RELOCATION != 0
	ref.Visit(visitor);
#endif
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceVal(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TValValue<T>::Type &ref)
{
	::CLRPrivate::ValTracer< ::CLRTI::TypeProtoTraits<T>::IsValueType, ::CLRTI::TypeTraits<T>::IsValueTraceable, T >::Trace(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceRef(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TRefValue<T>::Type &ref)
{
	::CLRUtil::RetargetRef<typename ::CLRVM::TValueObjectType<T>::Type>(visitor, ref);
}


#if CLARITY_PRECISE_TEMPORARY_MARKING

// Precise temporary marking tracers
template<class T>
inline void ::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::Temporary, T >::TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::Temporary, T >::TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::Temporary, T >::TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceVal(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::Temporary, T >::TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceRef(visitor, ref.m_value);
}

#endif

template<class T>
inline typename ::CLRVM::TRefValue<T>::Type (::CLRVM::AllocObject)(const ::CLRExec::Frame &frame)
{
    return typename ::CLRVM::TRefValue<T>::Type(frame.GetObjectManager()->AllocObject<T>(frame));
}

template<class T>
CLARITY_FORCEINLINE bool CompareEqual(const T &a, const T &b)
{
    return a == b;
}

template<class T>
CLARITY_FORCEINLINE bool ::CLRVM::IsZero(const typename ::CLRVM::TValValue<T>::Type &v)
{
    return v == typename ::CLRVM::TValValue<T>::Type(0);
}

template<class T>
CLARITY_FORCEINLINE bool ::CLRVM::IsNull(const typename ::CLRVM::TRefValue<T>::Type &v)
{
#if CLARITY_USE_STRICT_REFS != 0
	return v.IsNull();
#else
	return v == CLARITY_NULLPTR;
#endif
}


template<int TTypesAreSame, class TA, class TB>
CLARITY_FORCEINLINE bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<0, 0, TTypesAreSame, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Object-object
	return a == b;
}

template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<1, 0, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Interface-object
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcA = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TA>::Type *>(a);
	::CLRCore::GCObject *refB = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TB>::Type *>(b);

	if (ifcA == CLARITY_NULLPTR)
		return refB == CLARITY_NULLPTR;
	return ifcA->GetRootObject() == refB;
}

template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<0, 1, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Object-interface
	::CLRCore::GCObject *refA = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TA>::Type *>(a);
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcB = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TB>::Type *>(b);

	if (ifcB == CLARITY_NULLPTR)
		return refA == CLARITY_NULLPTR;
	return ifcB->GetRootObject() == refA;
}


template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<1, 1, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Interface-interface, different types
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcA = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TA>::Type *>(a);
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcB = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TB>::Type *>(b);

	if (ifcA == CLARITY_NULLPTR)
		return ifcB == CLARITY_NULLPTR;
	if (ifcB == CLARITY_NULLPTR)
		return false;

	return ifcB->GetRootObject() == ifcA->GetRootObject();
}

#endif
