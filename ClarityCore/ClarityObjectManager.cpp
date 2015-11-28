#include <stdlib.h>

#include "ClarityObjectManager.h"
#include "ClarityCppHelpers.h"
#include "ClarityHashMap.h"
#include "tSystem/tString.h"

void *CLRCore::ObjectManager::MemAlloc(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT size, bool movable)
{
	return malloc(size);
}

void CLRCore::ObjectManager::MemFree(void *ptr)
{
	free(ptr);
}

void ::CLRCore::ObjectManager::AddObject(::CLRCore::GCObject *obj)
{
}

::CLRCore::GCObject *::CLRCore::ObjectManager::GetStringConstant(const ::CLRExec::Frame &baseFrame, bool isPacked, ::CLRTypes::SizeT length, ::CLRTypes::S32 hash, const char *value)
{
	::CLRUtil::CppTracingFrame frame(baseFrame);
	::CLRUtil::CppTracedLocal< ::CLRX::NtSystem::tString > strRef(frame);
	::CLRUtil::CppTracedLocal< ::CLRCore::SZArray< ::CLRX::NtSystem::tChar > > charsArray(frame);

	strRef.Set(::CLRVM::AllocObject< ::CLRX::NtSystem::tString >(frame));
	charsArray.Set(::CLRVM::AllocSZArray< ::CLRX::NtSystem::tChar >(frame, length));

	strRef.Value()->ftm__chars.Set(charsArray.Value());

	//::CLRVM::TValValue< ::CLRX::NtSystem::tString >::Type strRef = this->AllocObject< ::CLRX::NtSystem::tString >(frame);
	return ::CLRVM::RefRootResolver< ::CLRX::NtSystem::tString >::Resolve(strRef.Value());
}

CLARITY_COREDLL ::CLRCore::IObjectManager *::CLRCore::CreateObjectManager()
{
	return new ::CLRCore::ObjectManager();
}
