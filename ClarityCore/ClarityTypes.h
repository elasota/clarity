#pragma once
#ifndef __CLARITY_TYPES_H__
#define __CLARITY_TYPES_H__

#include <stdint.h>
#include <cstddef>

namespace CLRTypes
{
    typedef bool Bool;
    typedef uint8_t U8;
    typedef uint16_t U16;
    typedef uint32_t U32;
    typedef uint64_t U64;
    typedef int8_t S8;
    typedef int16_t S16;
    typedef int32_t S32;
    typedef int64_t S64;

    typedef size_t SizeT;
    typedef ptrdiff_t PtrDiffT;
}

#define CLARITY_BOOLCONSTANT(n)     ((n) != 0)
#define CLARITY_INT8CONSTANT(n)     (static_cast< ::CLRTypes::S8 >(n))
#define CLARITY_UINT8CONSTANT(n)    (static_cast< ::CLRTypes::U8 >(n))
#define CLARITY_INT16CONSTANT(n)    (static_cast< ::CLRTypes::S16 >(n))
#define CLARITY_UINT16CONSTANT(n)   (static_cast< ::CLRTypes::U16 >(n))
#define CLARITY_INT32CONSTANT(n)    (static_cast< ::CLRTypes::S32 >(n))
#define CLARITY_UINT32CONSTANT(n)   (static_cast< ::CLRTypes::U32 >(n))
#define CLARITY_INT64CONSTANT(n)    (static_cast< ::CLRTypes::S32 >(n##LL))
#define CLARITY_UINT64CONSTANT(n)   (static_cast< ::CLRTypes::U32 >(n##ULL))

#endif
