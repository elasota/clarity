#pragma once
#ifndef __CLARITY_CPP_HELPERS_H__
#define __CLARITY_CPP_HELPERS_H__

#include "ClarityExec.h"
#include "ClarityVM.h"

namespace CLRUtil
{
	class CppTracingFrame;

	class CppTracedLocalBase
	{
		friend class CppTracingFrame;
	public:
		explicit CppTracedLocalBase(CppTracingFrame &cppTracingFrame);
		~CppTracedLocalBase();

		virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) CLARITY_PURE;

	private:
		CppTracedLocalBase(const CppTracedLocalBase &other);
		CppTracedLocalBase *Prev() const;

		CppTracingFrame &m_cppTracingFrame;
		CppTracedLocalBase *m_prev;
	};

	template<class T>
	class CppTracedLocal CLARITY_FINAL : public CppTracedLocalBase
	{
	public:
		explicit CppTracedLocal(CppTracingFrame &cppTracingFrame);
		explicit CppTracedLocal(CppTracingFrame &cppTracingFrame, const typename ::CLRVM::TValValue<T>::Type &defaultValue);
		virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) CLARITY_OVERRIDE;
		const typename ::CLRVM::TValValue<T>::Type &Value() const;
		void Set(const typename ::CLRVM::TValValue<T>::Type &value);

	private:
		CppTracedLocal(const CppTracedLocal<T> &other);

		typename ::CLRVM::TValValue<T>::Type m_value;
	};

	class CppTracingFrame CLARITY_FINAL
		: public ::CLRExec::Frame
	{
		friend class CppTracedLocalBase;

	public:
		explicit CppTracingFrame(const ::CLRExec::Frame &parentFrame);
		virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) const CLARITY_OVERRIDE;

	private:
		CppTracedLocalBase *TopStackedLocal() const;
		void Push(CppTracedLocalBase *newTop);
		void Pop();

		CppTracedLocalBase *m_stack;
	};
}

///////////////////////////////////////////////////////////////////////////////
#include <string.h>

inline ::CLRUtil::CppTracedLocalBase *::CLRUtil::CppTracedLocalBase::Prev() const
{
	return this->m_prev;
}

inline ::CLRUtil::CppTracedLocalBase::CppTracedLocalBase(CppTracingFrame &cppTracingFrame)
	: m_cppTracingFrame(cppTracingFrame)
	, m_prev(cppTracingFrame.TopStackedLocal())
{
	cppTracingFrame.Push(this);
}

inline ::CLRUtil::CppTracedLocalBase::~CppTracedLocalBase()
{
	m_cppTracingFrame.Pop();
}

template<class T>
inline ::CLRUtil::CppTracedLocal<T>::CppTracedLocal(CppTracingFrame &cppTracingFrame)
	: ::CLRUtil::CppTracedLocalBase(cppTracingFrame)
{
	memset(&this->m_value, 0, sizeof(this->m_value));
}

template<class T>
inline ::CLRUtil::CppTracedLocal<T>::CppTracedLocal(CppTracingFrame &cppTracingFrame, const typename ::CLRVM::TValValue<T>::Type &defaultValue)
	: ::CLRUtil::CppTracedLocalBase(cppTracingFrame)
	, m_value(defaultValue)
{
}

template<class T>
inline void ::CLRUtil::CppTracedLocal<T>::TouchReferences(::CLRExec::IRefVisitor &refVisitor)
{
	::CLRVM::TracerFuncs<T>::TraceVal(refVisitor, this->m_value);
}

template<class T>
inline const typename ::CLRVM::TValValue<T>::Type &::CLRUtil::CppTracedLocal<T>::Value() const
{
	return this->m_value;
}

template<class T>
inline void ::CLRUtil::CppTracedLocal<T>::Set(const typename ::CLRVM::TValValue<T>::Type &value)
{
	this->m_value = value;
#if CLARITY_INCREMENTAL_GC != 0
#error "Not implemented"
#endif
}

inline ::CLRUtil::CppTracingFrame::CppTracingFrame(const ::CLRExec::Frame &parentFrame)
	: ::CLRExec::Frame(parentFrame)
	, m_stack(CLARITY_NULLPTR)
{
}

inline void ::CLRUtil::CppTracingFrame::TouchReferences(::CLRExec::IRefVisitor &refVisitor) const
{
	::CLRUtil::CppTracedLocalBase *localVar = this->m_stack;
	while (localVar != CLARITY_NULLPTR)
	{
		localVar->TouchReferences(refVisitor);
		localVar = localVar->Prev();
	}
}

inline ::CLRUtil::CppTracedLocalBase *::CLRUtil::CppTracingFrame::TopStackedLocal() const
{
	return this->m_stack;
}

inline void ::CLRUtil::CppTracingFrame::Push(::CLRUtil::CppTracedLocalBase *newTop)
{
	this->m_stack = newTop;
}

inline void ::CLRUtil::CppTracingFrame::Pop()
{
	this->m_stack = this->m_stack->Prev();
}


#endif
