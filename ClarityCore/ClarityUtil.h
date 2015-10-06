#ifndef __CLARITY_UTIL_H__
#define __CLARITY_UTIL_H__

#include "ClarityCompilerDefs.h"

namespace CLRCore
{
    struct RefTarget;
}

namespace CLRUtil
{
    template<class T>
    struct Ref
    {
        typedef T* Type;

        CLARITY_FORCEINLINE static typename ::CLRUtil::Ref<T>::Type Null()
        {
            return CLARITY_NULLPTR;
        }

    private:
        Ref();
    };

    template<class T>
    struct DGBoundReturn
    {
        typedef T* Type;

    private:
        DGBoundReturn();
    };

    typedef ::CLRCore::RefTarget *DGTarget;

    template<int TIsValueType, class T>
    struct ValResolver
    {
    };

    template<class T>
    struct ValResolver < 1, T >
    {
        typedef T Type;
    };

    template<class T>
    struct ValResolver < 0, T >
    {
        typedef typename Ref<T>::Type *Type;
    };

    template<class T>
    struct Val
    {
        typedef typename ValResolver<::CLRTI::TypeTraits<T>::IsValueType, T>::Type Type;
    };


    template<class T>
    struct Boxed
    {
    private:
        Boxed();
    };

    template<class T>
    struct ConstrainedVtableGlue
    {
    private:
        ConstrainedVtableGlue();
    };

    template<class T>
    struct RefParameter
    {
        typedef T& Type;
    private:
        RefParameter();
    };
}

#endif
