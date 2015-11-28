#pragma once
#ifndef __CLARITY_OBJECTMANAGER_H__
#define __CLARITY_OBJECTMANAGER_H__

#include "ClarityCore.h"

namespace CLRCore
{
	class ObjectManager CLARITY_FINAL : public IObjectManager
	{
	public:
		virtual void *MemAlloc(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT size, bool movable) CLARITY_OVERRIDE;
		virtual void MemFree(void *ptr) CLARITY_OVERRIDE;
		virtual void AddObject(GCObject *obj) CLARITY_OVERRIDE;
		virtual ::CLRCore::GCObject *GetStringConstant(const ::CLRExec::Frame &frame, bool isPacked, ::CLRTypes::SizeT length, ::CLRTypes::S32 hash, const char *value) CLARITY_OVERRIDE;
		virtual GCObject *GetStaticClass(const ::CLRExec::Frame &frame, StaticCacheLocator &cacheLocator, TypeInfoQueryFunc rttiQuery) CLARITY_OVERRIDE { CLARITY_NOTIMPLEMENTED; }
	};
}

#endif
