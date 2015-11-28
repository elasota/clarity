#pragma once
#ifndef __CLARITY_CODESUPPORT_H__
#define __CLARITY_CODESUPPORT_H__

#include "ClarityCore.h"
#include "tSystem/tArray.Def.h"
#include "tSystem/tUIntPtr.Def.h"

namespace CLRUtil
{
	class ArrayContainerBase
		: public ::CLRX::NtSystem::tArray
	{
	public:
		::CLRVM::TValValue< ::CLRX::NtSystem::tUIntPtr >::Type Length() const;

	private:
		::CLRTypes::SizeT m_length;
	};

	template<class TArrayTag>
	class ArrayContainer
		: public ArrayContainerBase
	{

	};
}

namespace CLRVM
{
	template<class T>
	class ArrayLengthReader
		: public ::ClarityInternal::NoCreate
	{
	public:
		static typename ::CLRVM::TValValue< ::CLRX::NtSystem::tUIntPtr >::Type Read(const ::CLRExec::Frame& frame, const typename ::CLRVM::TRefValue<T>::Type& arrayRef);
	};
}

#endif
