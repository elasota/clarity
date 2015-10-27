#pragma once
#ifndef __CLARITY_CORE_H__
#define __CLARITY_CORE_H__

#include "ClarityCompilerDefs.h"
#include "ClarityTypes.h"

namespace CLRTI
{
    template<class T>
    struct TypeProtoTraits
    {
    };
    template<class T>
    struct TypeTraits
    {
    };
}

namespace CLRExec
{
    class Frame;
}

namespace CLRCore
{
    struct GCObject;

    struct RefTarget
    {
        virtual GCObject *GetRootRefTarget() CLARITY_PURE;
    };

    struct GCObject : public RefTarget
    {
        virtual GCObject *GetRootRefTarget() CLARITY_OVERRIDE;
    };

    template<class T>
    struct SZArray
    {
    };

    struct IObjectManager
    {
        virtual void *MemAlloc(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT size) CLARITY_PURE;
        virtual void AddObject(GCObject *obj) CLARITY_PURE;

        template<class T>
        T *AllocObject(const ::CLRExec::Frame &frame);
    };
}

template<class T>
struct ::CLRTI::TypeProtoTraits< ::CLRCore::SZArray<T> >
{
    enum
    {
        IsValueType = 0,
        IsArray = 1,
    };
};


#include "ClarityExec.h"
#include "ClarityUtil.h"
#include "ClarityCompilerDefs.h"
#include "ClarityVM.h"

#include <new>

inline ::CLRCore::GCObject *::CLRCore::GCObject::GetRootRefTarget()
{
    return this;
}

template<class T>
inline T *::CLRCore::IObjectManager::AllocObject(const ::CLRExec::Frame &frame)
{
    T *obj = static_cast<T*>(this->MemAlloc(frame, sizeof(T)));
    new (obj) T();
    return obj;
}


#endif
