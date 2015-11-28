#ifndef __CLARITY_EXEC_H__
#define __CLARITY_EXEC_H__

#include "ClarityCompilerDefs.h"
#include "ClarityInternalSupport.h"

#include "ClarityExec_Defs.h"


///////////////////////////////////////////////////////////////////////////////
CLARITY_FORCEINLINE ::CLRExec::Frame::Frame(::CLRCore::IObjectManager *objm)
    : m_parentFrame(CLARITY_NULLPTR)
    , m_objm(objm)
{
}

CLARITY_FORCEINLINE ::CLRExec::Frame::Frame(const ::CLRExec::Frame &parentFrame)
    : m_parentFrame(&parentFrame)
    , m_objm(parentFrame.m_objm)
{
}

CLARITY_FORCEINLINE ::CLRCore::IObjectManager *::CLRExec::Frame::GetObjectManager() const
{
    return m_objm;
}

inline ::CLRExec::ReadWriteMutex *::CLRExec::Frame::GetReadWriteMutex() const
{
	return CLARITY_NULLPTR;
}

template<class T>
CLARITY_FORCEINLINE ::CLRExec::TracingLocalFrame<T> (::CLRPrivate::TMaybeTracingLocalFrame_Disambiguation<0, T>::Disambiguate)(const ::CLRExec::Frame &frame, T& tracedLocals)
{
	return ::CLRExec::TracingLocalFrame<T>(frame, tracedLocals);
}

template<class T>
CLARITY_FORCEINLINE const ::CLRExec::Frame &(::CLRPrivate::TMaybeTracingLocalFrame_Disambiguation<1, T>::Disambiguate)(const ::CLRExec::Frame &frame, const T& tracedLocals)
{
    return frame;
}


CLARITY_FORCEINLINE ::CLRExec::RootLevelFrame::RootLevelFrame(::CLRCore::IObjectManager *objm)
    : ::CLRExec::Frame(objm)
{
}

inline void ::CLRExec::RootLevelFrame::TouchReferences(::CLRExec::IRefVisitor &refVisitor) const
{
}

template<class T>
CLARITY_FORCEINLINE ::CLRExec::TracingLocalFrame<T>::TracingLocalFrame(const ::CLRExec::Frame &parentFrame, T &tracingFrame)
    : ::CLRExec::Frame(parentFrame)
    , m_tracingFrame(tracingFrame)
{
}

template<class T>
CLARITY_FORCEINLINE void ::CLRExec::TracingLocalFrame<T>::TouchReferences(::CLRExec::IRefVisitor &refVisitor) const
{
    m_tracingFrame.VisitReferences(refVisitor);
}

#endif
