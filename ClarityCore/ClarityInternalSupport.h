#pragma once
#ifndef __CLARITY_INTERNAL_SUPPORT_H__
#define __CLARITY_INTERNAL_SUPPORT_H__

#include "ClarityCompilerDefs.h"

namespace ClarityInternal
{
    template<class T>
    struct TypeDef
    {
        typedef T Type;
    private:
        TypeDef();
    };

    struct NoCreate
    {
    private:
        NoCreate();
    };

    template<class T, int TExecute>
    struct ConditionalZeroFiller
        : public ::ClarityInternal::NoCreate
    {
    };

    template<class T>
    struct ConditionalZeroFiller<T, 0>
        : public ::ClarityInternal::NoCreate
    {
        static void ZeroFill(T &instance);
    };

    template<class T>
    struct ConditionalZeroFiller<T, 1>
        : public ::ClarityInternal::NoCreate
    {
        static void ZeroFill(T &instance);
    };
}

#include <string.h>

template<class T>
CLARITY_FORCEINLINE void ::ClarityInternal::ConditionalZeroFiller<T, 1>::ZeroFill(T &instance)
{
    memset(&instance, 0, sizeof(T))
};

template<class T>
CLARITY_FORCEINLINE void ::ClarityInternal::ConditionalZeroFiller<T, 0>::ZeroFill(T &instance)
{
};

#endif
