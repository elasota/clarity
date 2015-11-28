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

	typedef float F32;
	typedef double F64;

    typedef size_t SizeT;
    typedef ptrdiff_t PtrDiffT;

	typedef size_t AtomicCapableInt;

	typedef intptr_t IntPtr;
	typedef uintptr_t UIntPtr;

	template<class TTag, class TValue>
	class TypeTaggedNumber
	{
	public:
		typedef TValue TValueType;

		TypeTaggedNumber();
		TypeTaggedNumber(const TypeTaggedNumber<TTag, TValue>& other);
		explicit TypeTaggedNumber(const TValue& v);

		TypeTaggedNumber<TTag, TValue>& operator =(const TypeTaggedNumber<TTag, TValue>& other);
		bool operator ==(const TypeTaggedNumber<TTag, TValue>& other) const;
		bool operator !=(const TypeTaggedNumber<TTag, TValue>& other) const;

		const TValue& Value() const;

	private:
		TValue m_value;
	};

	template<class T>
	class NumberDetaggerBase
	{
	public:
		typedef T TDetaggedNumber;
		static T Detag(T v) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class TTag, class TValue>
	class NumberDetaggerBase< TypeTaggedNumber<TTag, TValue> >
	{
	public:
		typedef TValue TDetaggedNumber;
		static const TValue &Detag(const TypeTaggedNumber<TTag, TValue>& v) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	class NumberDetagger
		: public NumberDetaggerBase<T>
	{
	};

	template<class T>
	typename CLRTypes::NumberDetagger<T>::TDetaggedNumber DetagNumber(const T& v);
}

#define CLARITY_BOOLCONSTANT(n)     ((n) != 0)
#define CLARITY_INT8CONSTANT(n)     (static_cast< ::CLRTypes::S8 >(n))
#define CLARITY_UINT8CONSTANT(n)    (static_cast< ::CLRTypes::U8 >(n))
#define CLARITY_INT16CONSTANT(n)    (static_cast< ::CLRTypes::S16 >(n))
#define CLARITY_UINT16CONSTANT(n)   (static_cast< ::CLRTypes::U16 >(n))
#define CLARITY_INT32CONSTANT(n)    (static_cast< ::CLRTypes::S32 >(n))
#define CLARITY_UINT32CONSTANT(n)   (static_cast< ::CLRTypes::U32 >(n))
#define CLARITY_INT64CONSTANT(n)    (static_cast< ::CLRTypes::S64 >(n##LL))
#define CLARITY_UINT64CONSTANT(n)   (static_cast< ::CLRTypes::U64 >(n##LL))	// LL instead of ULL because the source is signed

///////////////////////////////////////////////////////////////////////////////
template<class TTag, class TValue>
CLARITY_FORCEINLINE CLRTypes::TypeTaggedNumber<TTag, TValue>::TypeTaggedNumber()
{
}

template<class TTag, class TValue>
CLARITY_FORCEINLINE CLRTypes::TypeTaggedNumber<TTag, TValue>::TypeTaggedNumber(const TypeTaggedNumber<TTag, TValue>& other)
	: m_value(other.m_value)
{
}

template<class TTag, class TValue>
CLARITY_FORCEINLINE CLRTypes::TypeTaggedNumber<TTag, TValue>::TypeTaggedNumber(const TValue& v)
	: m_value(v)
{
}

template<class TTag, class TValue>
CLARITY_FORCEINLINE::CLRTypes::TypeTaggedNumber<TTag, TValue>& ::CLRTypes::TypeTaggedNumber<TTag, TValue>::operator =(const TypeTaggedNumber<TTag, TValue>& other)
{
	m_value = other.m_value;
	return *this;
}

template<class TTag, class TValue>
CLARITY_FORCEINLINE bool (::CLRTypes::TypeTaggedNumber<TTag, TValue>::operator ==)(const ::CLRTypes::TypeTaggedNumber<TTag, TValue>& other) const
{
	return this->m_value == other.m_value;
}

template<class TTag, class TValue>
CLARITY_FORCEINLINE bool ::CLRTypes::TypeTaggedNumber<TTag, TValue>::operator !=(const ::CLRTypes::TypeTaggedNumber<TTag, TValue>& other) const
{
	return this->m_value != other.m_value;
}

template<class T>
CLARITY_FORCEINLINE typename CLRTypes::NumberDetagger<T>::TDetaggedNumber CLRTypes::DetagNumber(const T& v)
{
	return CLRTypes::NumberDetagger<T>::Detag(v);
}


#endif
