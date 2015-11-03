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
	struct IRefVisitor;
}

namespace CLRX
{
	namespace NtSystem
	{
		struct tObject;
	}
}

namespace CLRCore
{
    struct GCObject;

    struct RefTarget
    {
        virtual ::CLRX::NtSystem::tObject *GetRootObject() CLARITY_PURE;
		virtual ::CLRCore::GCObject *GetRootRefTarget() CLARITY_PURE;
    };

    struct GCObject : public RefTarget
    {
		virtual void VisitReferences(::CLRExec::IRefVisitor &visitor) CLARITY_PURE;
    };

	class ArrayInfoBlock
	{
	public:
		::CLRCore::GCObject *GetObject() const;
		::CLRTypes::SizeT GetDimension(::CLRTypes::SizeT index) const;
		void *GetStorage() const;

	private:
		const ::CLRTypes::SizeT *m_dimensions;
		void *m_storage;
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

namespace CLRTI
{
	template<class T>
	struct TypeProtoTraits< ::CLRCore::SZArray<T> >
	{
		enum
		{
			IsValueType = 0,
			IsArray = 1,
			IsInterface = 0,
			IsDelegate = 0,
			IsMulticastDelegate = 0,
			IsEnum = 0,
			IsReferenceArray = (::CLRTI::TypeProtoTraits<T>::IsValueType == 0) ? 1 : 0,
		};
	};
}


#include "ClarityExec.h"
#include "ClarityUtil.h"
#include "ClarityCompilerDefs.h"
#include "ClarityVM.h"

#include <new>

template<class T>
inline T *::CLRCore::IObjectManager::AllocObject(const ::CLRExec::Frame &frame)
{
    T *obj = static_cast<T*>(this->MemAlloc(frame, sizeof(T)));
    new (obj) T();
    return obj;
}


#endif
