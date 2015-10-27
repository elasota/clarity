#pragma once
#ifndef __CLARITY_VM_H__
#define __CLARITY_VM_H__

#include "ClarityConfig.h"
#include "ClarityInternalSupport.h"

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
    struct TRefValueResolver_IsValueType
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TRefValueResolver_IsValueType<T, 1>
        : public ::ClarityInternal::TypeDef< typename ::CLRUtil::TRef< ::CLRUtil::Boxed<T> >::Type >
    {
    };

    template<class T>
    struct TRefValueResolver_IsValueType<T, 0>
        : public ::ClarityInternal::TypeDef< typename ::CLRUtil::TRef<T>::Type >
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
    struct TRefValue
        : public ::ClarityInternal::TypeDef<typename ::CLRPrivate::TRefValueResolver_IsValueType<T, ::CLRTI::TypeProtoTraits<T>::IsValueType>::Type>
    {
    };

    template<class T>
    struct TValValue
        : public ::ClarityInternal::TypeDef<typename ::CLRPrivate::TValValueResolver_IsValueType<T, ::CLRTI::TypeProtoTraits<T>::IsValueType>::Type>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tByte>
        : public ::ClarityInternal::TypeDef<::CLRTypes::U8>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tSByte>
        : public ::ClarityInternal::TypeDef<::CLRTypes::S8>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tInt16>
        : public ::ClarityInternal::TypeDef<::CLRTypes::S16>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tUInt16>
        : public ::ClarityInternal::TypeDef<::CLRTypes::U16>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tInt32>
        : public ::ClarityInternal::TypeDef<::CLRTypes::S32>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tUInt32>
        : public ::ClarityInternal::TypeDef<::CLRTypes::U32>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tInt64>
        : public ::ClarityInternal::TypeDef<::CLRTypes::S64>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tUInt64>
        : public ::ClarityInternal::TypeDef<::CLRTypes::U64>
    {
    };

    template<>
    struct TValValue<::CLRX::NtSystem::tBoolean>
        : public ::ClarityInternal::TypeDef<::CLRTypes::Bool>
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
    typename void KillVReg(TLocalType &local);

    template<class TLocalType>
    typename TLocalType::LocalValueType &VRegValue(TLocalType &local);

    template<class TA, class TB>
    bool CompareEqualReferences(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);

    template<class T>
    bool IsZero(const T &a);
}

///////////////////////////////////////////////////////////////////////////////
#include "ClarityExec.h"

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
CLARITY_FORCEINLINE typename ::CLRUtil::TRef<T>::Type (::CLRVM::ParamThis<T>)(T *bThis)
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
    visitor.Test();
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type &ref)
{
    visitor.Test();
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceVal(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TValValue<T>::Type &ref)
{
    visitor.Test();
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceRef(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TRefValue<T>::Type &ref)
{
    visitor.Test();
}


#if CLARITY_PRECISE_TEMPORARY_MARKING

// Precise temporary marking tracers
template<class T>
inline void ::CLRVM::LocalTracerFuncs<::CLRVM::ELocalType::Temporary, T>::TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<::CLRVM::ELocalType::Temporary, T>::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs<::CLRVM::ELocalType::Temporary, T>::TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<::CLRVM::ELocalType::Temporary, T>::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs<::CLRVM::ELocalType::Temporary, T>::TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<::CLRVM::ELocalType::Temporary, T>::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceVal(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs<::CLRVM::ELocalType::Temporary, T>::TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<::CLRVM::ELocalType::Temporary, T>::Type &ref)
{
    if (ref.m_isAlive)
        ::CLRVM::TracerFuncs<T>::TraceRef(visitor, ref.m_value);
}

#endif

template<class T>
inline typename ::CLRVM::TRefValue<T>::Type (::CLRVM::AllocObject<T>)(const ::CLRExec::Frame &frame)
{
    return typename ::CLRVM::TRefValue<T>::Type(frame.GetObjectManager()->AllocObject<T>(frame));
}

template<class T>
CLARITY_FORCEINLINE bool CompareEqual(const T &a, const T &b)
{
    return a == b;
}

template<class T>
CLARITY_FORCEINLINE bool IsZero(const T &v)
{
    return v == 0;
}

template<class T>
CLARITY_FORCEINLINE bool IsNotZero(const T &v)
{
    return v == 0;
}



#endif
