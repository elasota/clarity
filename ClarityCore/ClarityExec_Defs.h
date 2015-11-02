#pragma once
#ifndef __CLARITY_EXEC_DEFS_H__
#define __CLARITY_EXEC_DEFS_H__

#include "ClarityCompilerDefs.h"

namespace CLRUtil
{
    template<class T> struct TRef;
    template<class T> struct Boxed;
    template<class T> struct TValueThisParameter;
    template<class T> struct TAnchoredManagedPtr;
}

namespace CLRExec
{
    template<class T> class TracingLocalFrame;
    class Frame;
    struct IRefVisitor;
}

namespace CLRCore
{
    struct IObjectManager;
    struct RefTarget;
}

namespace CLRPrivate
{
    template<int TIsTraced, class T>
    struct TMaybeTracingLocalFrame_Disambiguation
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct TMaybeTracingLocalFrame_Disambiguation<0, T>
        : public ::ClarityInternal::TypeDef< ::CLRExec::TracingLocalFrame<T> >
    {
        static ::CLRExec::TracingLocalFrame<T> Disambiguate(const ::CLRExec::Frame &frame, T& tracedLocals);
    };

    template<class T>
    struct TMaybeTracingLocalFrame_Disambiguation<1, T>
        : public ::ClarityInternal::TypeDef<const ::CLRExec::Frame &>
    {
        static const ::CLRExec::Frame &Disambiguate(const ::CLRExec::Frame &frame, const T& tracedLocals);
    };
}

namespace CLRExec
{
    class Frame
    {
    public:
        explicit Frame(const ::CLRExec::Frame &parentFrame);
        explicit Frame(::CLRCore::IObjectManager *objm);

        virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) const CLARITY_PURE;
        ::CLRCore::IObjectManager *GetObjectManager() const;

    private:
        Frame();
        const ::CLRExec::Frame *m_parentFrame;
        ::CLRCore::IObjectManager *m_objm;
    };

    class RootLevelFrame CLARITY_FINAL
        : public ::CLRExec::Frame
    {
    public:
        explicit RootLevelFrame(::CLRCore::IObjectManager *objm);
        virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) const CLARITY_OVERRIDE;
    };

    template<class T>
    class UnboxThunkFrame : public ::CLRExec::Frame
    {
    public:
        UnboxThunkFrame(const ::CLRExec::Frame &parentFrame, const typename ::CLRUtil::TAnchoredManagedPtr<T>::Type &thisAnchor);

        virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) const CLARITY_OVERRIDE CLARITY_FINAL;
        typename ::CLRUtil::TValueThisParameter<T>::Type GetPassableThis() const;
    };

    template<class T>
    class TracingLocalFrame CLARITY_FINAL
        : public ::CLRExec::Frame
    {
    public:
        TracingLocalFrame(const ::CLRExec::Frame &parentFrame, T &tracingFrame);
        virtual void TouchReferences(::CLRExec::IRefVisitor &refVisitor) const CLARITY_OVERRIDE CLARITY_FINAL;

    private:
        T &m_tracingFrame;
    };

    struct IRefVisitor
    {
		virtual ::CLRCore::RefTarget *TouchReference(::CLRCore::RefTarget *refTarget) CLARITY_PURE;
    };

    template<class T>
    struct TMaybeTracingLocalFrame
        : public ::ClarityInternal::TypeDef<typename ::CLRPrivate::TMaybeTracingLocalFrame_Disambiguation<T::IsTraceable, T> >
    {
    };
}

#endif
