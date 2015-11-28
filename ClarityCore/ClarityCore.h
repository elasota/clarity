#pragma once
#ifndef __CLARITY_CORE_H__
#define __CLARITY_CORE_H__

#include "ClarityCompilerDefs.h"
#include "ClarityTypes.h"
#include "ClarityInternalSupport.h"
#include "ClarityProjects.h"

// ****************************** PROTOTYPES ******************************
namespace CLRTI
{
    template<class T>
    struct TypeProtoTraits
    {
    };
    template<class T>
    struct TypeTraits
    {
    };
	template<class TSource, class TDest>
	struct TypeRefCompatibility
	{
		enum
		{
			IsAssignable = 0,
		};
	};
}

namespace CLRExec
{
    class Frame;
	struct IRefVisitor;
}

namespace CLRX
{
	namespace NtSystem
	{
		struct tObject;
	}
}

// ****************************** DEFS ******************************
namespace CLRCore
{
    class GCObject;
	class ObjectManager;
	struct TypeInfo;
	struct StaticCacheLocator;

	typedef void (*TypeInfoQueryFunc)(TypeInfo &typeInfo);

	//////////////////////////////////////////////////////////////////////
	// RefTarget
	// Represents the pointee of any reference.  All object and interface types derive from RefTarget.
    struct RefTarget
    {
		virtual ::CLRCore::GCObject *GetRootGCObject() CLARITY_PURE;
    };

	//////////////////////////////////////////////////////////////////////
	// InterfaceTarget
	// Root class of all interfaces.
	struct InterfaceTarget : public RefTarget
	{
		virtual ::CLRX::NtSystem::tObject *GetRootObject() CLARITY_PURE;
	};

	//////////////////////////////////////////////////////////////////////
	// GCObject
	// Root class of all classes.
	class GCObject : public RefTarget
    {
		friend class ::CLRCore::ObjectManager;

	public:
		virtual void VisitReferences(::CLRExec::IRefVisitor &visitor) CLARITY_PURE;

	private:
		void InitGCObject(::CLRTypes::SizeT size);

		::CLRTypes::SizeT m_size;
    };

	//////////////////////////////////////////////////////////////////////
	// StaticFieldContainer
	// Root class of all static field containers.
	class StaticFieldContainer : public GCObject
	{
	public:
		virtual ::CLRCore::GCObject *GetRootGCObject() CLARITY_OVERRIDE CLARITY_FINAL;
	};

	//////////////////////////////////////////////////////////////////////
	// ArrayInfoBlock
	// Root class of all array types.
	class ArrayInfoBlock
	{
	public:
		::CLRCore::GCObject *GetObject() const;
		::CLRTypes::SizeT GetDimension(::CLRTypes::SizeT index) const;
		void *GetStorage() const;

	private:
		const ::CLRTypes::SizeT *m_dimensions;
		void *m_storage;
	};

	//////////////////////////////////////////////////////////////////////
	// SZArray
	// Tag-only type for single-dimension 0-bounded arrays
	template<class T>
	struct SZArray
	{
		typedef T TSubscriptType;

		static ::CLRTypes::SizeT ComputeSize(const ::CLRExec::Frame &frame);
	};

	//////////////////////////////////////////////////////////////////////
	// IObjectManager
	// Interface to a CLR object manager.
	struct IObjectManager
    {
        virtual void *MemAlloc(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT size, bool movable) CLARITY_PURE;
		virtual void MemFree(void *ptr) CLARITY_PURE;
        virtual void AddObject(GCObject *obj) CLARITY_PURE;
		virtual GCObject *GetStringConstant(const ::CLRExec::Frame &frame, bool isPacked, ::CLRTypes::SizeT length, ::CLRTypes::S32 hash, const char *value) CLARITY_PURE;
		virtual GCObject *GetStaticClass(const ::CLRExec::Frame &frame, StaticCacheLocator &cacheLocator, TypeInfoQueryFunc rttiQuery) CLARITY_PURE;

        template<class T>
        T *AllocObject(const ::CLRExec::Frame &frame);
    };

	//////////////////////////////////////////////////////////////////////
	// StaticCacheLocator
	// Global variable type used to store a set-once lookup into the object manager's static cache.
	struct StaticCacheLocator
	{
		::CLRTypes::U32 m_cacheHash;
		::CLRTypes::AtomicCapableInt m_cacheID;
	};

	CLARITY_COREDLL ::CLRCore::IObjectManager *CreateObjectManager();
}

namespace CLRTI
{
	template<class T>
	struct TypeProtoTraits< ::CLRCore::SZArray<T> >
	{
		enum
		{
			IsValueType = 0,
			IsArray = 1,
			IsInterface = 0,
			IsDelegate = 0,
			IsMulticastDelegate = 0,
			IsEnum = 0,
			IsReferenceArray = (::CLRTI::TypeProtoTraits<T>::IsValueType == 0) ? 1 : 0,
		};
	};

	template<class T>
	struct TypeTraits< ::CLRCore::SZArray<T> >
	{
		enum
		{
			IsValueTraceable = 1,
		};
	};
}


#include "ClarityExec.h"

namespace CLRVM
{
	template<class T> struct TMaybeAnchoredManagedPtr;
	template<class T> struct TValueObjectType;
	template<class T> struct TRefValue;
	template<class T> struct TValValue;
}

namespace CLRUtil
{
	template<class T> struct TSimpleRef;

#if CLARITY_USE_STRICT_REFS != 0
	template<class T> class StrictRef;
#endif

	template<class T> class RefArrayReference;

	typedef ::CLRCore::RefTarget *TDGTarget;
}

namespace CLRPrivate
{
	template<class T>
	struct SimpleRefVisitor
	{
		static T *VisitObject(::CLRExec::IRefVisitor &visitor, T *ref);
		static T *VisitInterface(::CLRExec::IRefVisitor &visitor, T *ref);

#if CLARITY_USE_STRICT_REFS != 0
		static CLRUtil::StrictRef<T> VisitObject(::CLRExec::IRefVisitor &visitor, const CLRUtil::StrictRef<T> &ref);
		static CLRUtil::StrictRef<T> VisitInterface(::CLRExec::IRefVisitor &visitor, const CLRUtil::StrictRef<T> &ref);
#endif
	};

	template<int TIsValueType, int TIsInterface, class T>
	struct DelegateTargetConverter_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<1, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef ::CLRUtil::Boxed<T> *TResolvedTarget;

		static ::CLRUtil::Boxed<T> *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef T *TResolvedTarget;

		static T *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	template<class T>
	struct DelegateTargetConverter_ByTraits<0, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		typedef T *TResolvedTarget;

		static T *FromTarget(::CLRUtil::TDGTarget dgTarget);
		static ::CLRUtil::TDGTarget ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	// Delegate parameter converter

	template<int TSourceIsValueType, int TDestIsValueType, class TSource, class TDest>
	struct DelegateVarianceConverter_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	// Type sameness is enforced for delegates - They can not be variant even if passive conversions are possible
	template<class T>
	struct DelegateVarianceConverter_ByTraits<1, 1, T, T>
		: public ::ClarityInternal::NoCreate
	{
		static const typename ::CLRVM::TValValue<T>::Type &Convert(const typename ::CLRVM::TValValue<T>::Type &value);
	};

	// Type sameness is enforced for delegates - They can not be variant even if passive conversions are possible
	template<class TSource, class TDest>
	struct DelegateVarianceConverter_ByTraits<0, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static const typename ::CLRVM::TValValue<TSource>::Type &Convert(const typename ::CLRVM::TValValue<TSource>::Type &value);
	};

	// RefArray to RefArray conversion
	template<int TTypesAreSame, class TSource, class TDest>
	struct PassiveReferenceConverter_RefArrayRefArray_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_RefArrayRefArray_ByTraits<0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class T>
	struct PassiveReferenceConverter_RefArrayRefArray_ByTraits<1, T, T>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<T>::Type Convert(const typename ::CLRVM::TRefValue<T>::Type &ref);
	};

	// Object to object ref conversion
	template<int TSourceIsRefArray, int TDestIsRefArray, class TSource, class TDest>
	struct PassiveReferenceConverter_ObjObj_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ObjObj_ByTraits<0, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ObjObj_ByTraits<1, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ObjObj_ByTraits<1, 1, TSource, TDest>
		: public ::CLRPrivate::PassiveReferenceConverter_RefArrayRefArray_ByTraits<
		::ClarityInternal::AreTypesSame<TSource, TDest>::Value,
		TSource,
		TDest
		>
	{
	};

	template<int TSourceIsInterface, int TDestIsInterface, class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	// Ref to ref conversion
	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<0, 0, TSource, TDest>
		: public ::CLRPrivate::PassiveReferenceConverter_ObjObj_ByTraits<
		::CLRTI::TypeProtoTraits<TSource>::IsReferenceArray,
		::CLRTI::TypeProtoTraits<TDest>::IsReferenceArray,
		TSource,
		TDest
		>
	{
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<0, 1, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<1, 0, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter_ByTraits<1, 1, TSource, TDest>
		: public ::ClarityInternal::NoCreate
	{
		static typename ::CLRVM::TRefValue<TDest>::Type Convert(const typename ::CLRVM::TRefValue<TSource>::Type &ref);
	};

	template<int TIsReferenceArray, class T>
	struct TRef_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

#if CLARITY_USE_STRICT_REFS
	template<class T>
	struct TRef_ByTraits<0, T>
		: public ::ClarityInternal::TypeDef< ::CLRUtil::StrictRef<T> >
	{
	};
#else
	template<class T>
	struct TRef_ByTraits<0, T>
		: public ::ClarityInternal::TypeDef<T*>
	{
	};
#endif

	template<class T>
	struct TRef_ByTraits<1, T>
		: public ::ClarityInternal::TypeDef< ::CLRUtil::RefArrayReference<T> >
	{
	};
}

namespace CLRUtil
{
#if CLARITY_USE_STRICT_REFS != 0
	template<class T>
	class StrictRef
	{
	public:
		StrictRef();
		StrictRef(const StrictRef &other);
		explicit StrictRef(T *ptr);
		StrictRef<T> &operator =(const StrictRef<T>& other);
		bool operator ==(const StrictRef<T>& other) const;
		T *operator ->() const;
		bool IsNull() const;
		T *GetPtr() const;

	private:
		T *m_ptr;
	};

	template<class T>
	struct TSimpleRef
		: public ::ClarityInternal::TypeDef< StrictRef<T> >
	{
	};

	// Note: This only works with object references, NOT reference array references!
	template<class T>
	T *SimpleRefToPtr(const StrictRef<T> &ref);

#else
	template<class T>
	struct TRef
		: public ::ClarityInternal::TypeDef<T*>
	{
	};

	template<class T>
	T *RefToPtr(T *ref);
#endif

	typedef ::CLRCore::RefTarget *(*ReferenceConversionFunc)(::CLRCore::RefTarget *storedTarget);

	template<class T>
	class RefArrayReference
	{
	public:
		typedef T TSubscriptType;
	private:
		::CLRCore::ArrayInfoBlock *m_array;
		ReferenceConversionFunc m_convFunc;
	};

	template<class TSource, class TDest>
	struct PassiveReferenceConverter
		: public ::CLRPrivate::PassiveReferenceConverter_ByTraits< ::CLRTI::TypeProtoTraits<TSource>::IsInterface, ::CLRTI::TypeProtoTraits<TDest>::IsInterface, TSource, TDest >
	{
	};

	template<class T>
	struct DelegateTargetConverter
		: public ::CLRPrivate::DelegateTargetConverter_ByTraits< ::CLRTI::TypeProtoTraits<T>::IsValueType, ::CLRTI::TypeProtoTraits<T>::IsInterface, T >
	{
	};

	template<class TSource, class TDest>
	struct DelegateVarianceConverter
		: public ::CLRPrivate::DelegateVarianceConverter_ByTraits<
		::CLRTI::TypeProtoTraits<TSource>::IsValueType,
		::CLRTI::TypeProtoTraits<TDest>::IsValueType,
		TSource,
		TDest
		>
	{
	};

	template<class T>
	class AnchoredManagedPtr
	{
	public:
		void Visit(::CLRExec::IRefVisitor &refVisitor);

	private:
		::CLRCore::RefTarget *m_object;
		T *m_value;
	};

	template<class T>
	struct TAnchoredManagedPtr
		: public ::ClarityInternal::TypeDef< typename ::CLRUtil::AnchoredManagedPtr<T> >
	{
	};

	template<class T>
	struct TDGBoundReturn
	{
		typedef T* Type;

	private:
		TDGBoundReturn();
	};

	// Boxed<T>::Type is a container of a boxed value of type T
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
	struct TRefParameter
		: public ::ClarityInternal::TypeDef<T&>
	{
	};

	// ValueThisParameter<T>::Type is the type passed as a "this" pointer to a method of a value type
	template<class T>
	struct TValueThisParameter
		: public ::ClarityInternal::TypeDef<typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type>
	{
	};


	////////////////////////////////////////////////////////////////////////////////
	// Passive value loaders
	template<class TSource>
	struct PassiveValueConversionLoader { };

	template<class TSource, class TMid>
	struct PassiveValueSimpleConversionLoader
	{
		typedef TMid MidType;
		static TMid ToMid(TSource src);
	};

	template<>
	struct PassiveValueConversionLoader< ::CLRTypes::Bool >
	{
		typedef ::CLRTypes::S32 MidType;
		static ::CLRTypes::S32 ToMid(::CLRTypes::Bool src);
	};

	template<> struct PassiveValueConversionLoader< ::CLRTypes::U8 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::U8, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::U16 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::U16, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::U32 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::U32, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::U64 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::U64, ::CLRTypes::S64 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::S8 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::S8, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::S16 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::S16, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::S32 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::S32, ::CLRTypes::S32 > { };
	template<> struct PassiveValueConversionLoader< ::CLRTypes::S64 > : public PassiveValueSimpleConversionLoader< ::CLRTypes::S64, ::CLRTypes::S64 > { };

	////////////////////////////////////////////////////////////////////////////////
	template<class TMid, class TDest>
	struct PassiveValueSimpleConversionWriter
	{
		static TDest FromMid(TMid src);
	};

	template<class TSource>
	struct PassiveValueConversionWriter { };

	template<>
	struct PassiveValueConversionWriter< ::CLRTypes::Bool >
	{
		static ::CLRTypes::Bool FromMid(::CLRTypes::S32 src);
	};

	template<> struct PassiveValueConversionWriter< CLRTypes::U8 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::U8 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::U16 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::U16 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::U32 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::U32 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::U64 > : public PassiveValueSimpleConversionWriter< CLRTypes::S64, CLRTypes::U64 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::S8 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::S8 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::S16 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::S16 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::S32 > : public PassiveValueSimpleConversionWriter< CLRTypes::S32, CLRTypes::S32 > { };
	template<> struct PassiveValueConversionWriter< CLRTypes::S64 > : public PassiveValueSimpleConversionWriter< CLRTypes::S64, CLRTypes::S64 > { };

	template<class T>
	typename ::CLRUtil::TAnchoredManagedPtr<T>::Type Unbox(::CLRUtil::Boxed<T> *box);

	template<class TSource, class TDest>
	struct PassiveValueConverter
		: public ::ClarityInternal::NoCreate
	{
		static typename CLRVM::TValValue<TDest>::Type Convert(typename CLRVM::TValValue<TSource>::Type ref);
	};

	template<class T>
	typename CLRVM::TRefValue<T>::Type NullReference();
}




namespace CLRX
{
	namespace NtSystem
	{
		struct tBoolean;
		struct tByte;
		struct tSByte;
		struct tInt16;
		struct tUInt16;
		struct tInt32;
		struct tUInt32;
		struct tInt64;
		struct tUInt64;
		struct tChar;
		struct tIntPtr;
		struct tUIntPtr;
		struct tSingle;
		struct tDouble;
	}
}

namespace CLRUtil
{
	template<class T> struct TLocalManagedPtr;
	template<class T> struct TAnchoredManagedPtr;
	template<class T> struct TSimpleRef;
}

namespace CLRVM
{
	template<class T> struct TransientLocal;
	template<class T> struct NoInitPermanentLocal;
	template<class T> struct InitPermanentLocal;
}

namespace CLRCore
{
	struct RefTarget;
	template<class T> struct ValueArrayContainer;
	template<class T> struct RefArrayContainer;
	struct RefArrayContainerBase;
	struct ValueArrayContainerBase;
}

namespace CLRExec
{
	class Frame;
}

namespace CLRPrivate
{
	template<class TValueType>
	struct VRegOps_ByValueType
	{
		static TValueType &Liven(::CLRVM::NoInitPermanentLocal<TValueType> &local);
		static TValueType &Liven(::CLRVM::TransientLocal<TValueType> &local);
		static void Kill(::CLRVM::NoInitPermanentLocal<TValueType> &local);
		static void Kill(::CLRVM::TransientLocal<TValueType> &local);
		static TValueType &KillAndReturn(::CLRVM::NoInitPermanentLocal<TValueType> &local);
		static TValueType &KillAndReturn(::CLRVM::TransientLocal<TValueType> &local);
	};

	template<class T, int TIsValueType, int TIsArray, int TIsRefArray>
	struct TValueObjectTypeResolver_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TValueObjectTypeResolver_ByTraits<T, 1, 0, 0>
		: public ::ClarityInternal::TypeDef< ::CLRUtil::Boxed<T> >
	{
	};

	template<class T>
	struct TValueObjectTypeResolver_ByTraits<T, 0, 0, 0>
		: public ::ClarityInternal::TypeDef< T >
	{
	};

	template<class T>
	struct TValueObjectTypeResolver_ByTraits<T, 0, 1, 0>
		: public ::ClarityInternal::TypeDef< CLRCore::ValueArrayContainer<T> >
	{
	};

	// NOTE: There is no ValueObjectType for reference array references!

	//////////////////////////////////////////////////////////////////////
	template<class T, int TIsValueType, int TIsArray, int TIsRefArray>
	struct RefRootResolver_ByTraits
	{
	};

	template<class T>
	struct RefRootResolver_ByTraits<T, 1, 0, 0>
	{
		typedef CLRUtil::Boxed<T> TRootObjectType;
		static TRootObjectType *Resolve(const typename CLRUtil::TSimpleRef<TRootObjectType>::Type &ref) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	struct RefRootResolver_ByTraits<T, 0, 0, 0>
	{
		typedef T TRootObjectType;
		static TRootObjectType *Resolve(const typename CLRUtil::TSimpleRef<TRootObjectType>::Type &ref) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	struct RefRootResolver_ByTraits<T, 0, 1, 0>
	{
		typedef CLRCore::ValueArrayContainer<typename T::TSubscriptType> TRootObjectType;
		static TRootObjectType *Resolve(const typename CLRUtil::TSimpleRef<TRootObjectType>::Type &ref) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	struct RefRootResolver_ByTraits<T, 0, 1, 1>
	{
		typedef CLRCore::RefArrayContainerBase TRootObjectType;
		static TRootObjectType *Resolve(const typename CLRUtil::RefArrayReference<T> &ref) { CLARITY_NOTIMPLEMENTED; }
	};

	//////////////////////////////////////////////////////////////////////////////

	template<int TIsValueType, int TIsArray, int TIsRefArray, class T>
	struct TRefTypeValueResolver_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TRefTypeValueResolver_ByTraits<0, 0, 0, T>
		: public ::ClarityInternal::TypeDef< typename ::CLRUtil::TSimpleRef<T>::Type >
	{
	};

	template<class T>
	struct TRefTypeValueResolver_ByTraits<0, 1, 0, T>
		: public ::ClarityInternal::TypeDef< typename ::CLRUtil::TSimpleRef< ::CLRCore::ValueArrayContainer<typename T::TSubscriptType> >::Type >
	{
	};

	template<class T>
	struct TRefTypeValueResolver_ByTraits<0, 1, 1, T>
		: public ::ClarityInternal::TypeDef< ::CLRUtil::RefArrayReference<T> >
	{
	};

	template<class T>
	struct TRefTypeValueResolver_ByTraits<1, 0, 0, T>
		: public ::ClarityInternal::TypeDef< typename CLRUtil::TSimpleRef< CLRUtil::Boxed<T> >::Type >
	{
	};

	//////////////////////////////////////////////////////////////////////////////

	template<class T, int TIsValueType>
	struct TValValueResolver_IsValueType
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TValValueResolver_IsValueType<T, 1>
		: public ::ClarityInternal::TypeDef< T >
	{
	};

	template<class T>
	struct TValValueResolver_IsValueType<T, 0>
		: public TRefTypeValueResolver_ByTraits
		<
		0,
		::CLRTI::TypeProtoTraits<T>::IsArray,
		::CLRTI::TypeProtoTraits<T>::IsReferenceArray,
		T
		>
	{
	};

	//////////////////////////////////////////////////////////////////////////////
	template<int TIsValueType, int TIsInterface, int TIsArray, int TIsRefArray, class T>
	struct RefTracer
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct RefTracer<0, 0, 0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef<T>::Type &ref);
	};

	template<class T>
	struct RefTracer<0, 1, 0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef<T>::Type &ref);
	};

	template<class T>
	struct RefTracer<0, 0, 1, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef< CLRCore::ValueArrayContainer<typename T::TSubscriptType> >::Type &ref);
	};

	template<class T>
	struct RefTracer<0, 0, 1, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::RefArrayReference< T > &ref);
	};

	template<class T>
	struct RefTracer<1, 0, 0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(CLRExec::IRefVisitor &visitor, const typename CLRVM::TRefValue<T>::Type &ref);
	};

	////////////////////////////////////////////////////////////////////////////////
	template<int TIsValueType, int TIsInterface, int TIsArray, int TIsRefArray, int TIsValueTraceable, class T>
	struct ValTracer
		: public ::ClarityInternal::NoCreate
	{
	};

	template<int TIsInterface, int TIsArray, int TIsRefArray, class T>
	struct ValTracer<0, TIsInterface, TIsArray, TIsRefArray, 1, T>
		: public RefTracer<0, TIsInterface, TIsArray, TIsRefArray, T>
	{
	};

	template<class T>
	struct ValTracer<1, 0, 0, 0, 0, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(::CLRExec::IRefVisitor &visitor, const typename ::CLRVM::TValValue<T>::Type &ref);
	};

	template<class T>
	struct ValTracer<1, 0, 0, 0, 1, T>
		: public ::ClarityInternal::NoCreate
	{
		static void Trace(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TValValue<T>::Type &ref);
	};

	////////////////////////////////////////////////////////////////////////////////
	template<int TAIsInterface, int TBIsInterface, int TTypesAreSame, class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits
		: public ::ClarityInternal::NoCreate
	{
	};

	template<int TTypesAreSame, class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<0, 0, TTypesAreSame, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<1, 0, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<0, 1, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class TA, class TB>
	struct ReferenceEqualityComparer_ByTraits<1, 1, 0, TA, TB>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b);
	};

	template<class T>
	struct ReferenceEqualityComparer_ByTraits<1, 1, 1, T, T>
		: public ::ClarityInternal::NoCreate
	{
		static bool AreEqual(const typename ::CLRVM::TRefValue<T>::Type &a, const typename ::CLRVM::TRefValue<T>::Type &b);
	};

	template<int TIsRefArray, class T>
	struct ArrayRefPackager_ByTraits
	{
	};

	template<class T>
	struct ArrayRefPackager_ByTraits<0, T>
	{
		static typename CLRUtil::TSimpleRef< ::CLRCore::ValueArrayContainer<typename T::TSubscriptType> >::Type PackageRef(CLRCore::ValueArrayContainer<typename T::TSubscriptType> *arrayRef) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	struct ArrayRefPackager_ByTraits<1, T>
	{
		static CLRUtil::RefArrayReference<T> PackageRef(CLRCore::RefArrayContainer<T> *arrayRef) { CLARITY_NOTIMPLEMENTED; }
	};
}

namespace CLRVM
{

	template<class T>
	struct RefRootResolver
		: public CLRPrivate::RefRootResolver_ByTraits<
			T,
			CLRTI::TypeProtoTraits<T>::IsValueType,
			CLRTI::TypeProtoTraits<T>::IsArray,
			CLRTI::TypeProtoTraits<T>::IsReferenceArray
		>
	{
	};

	template<class T>
	struct TransientLocal
	{
#if CLARITY_PRECISE_TEMPORARY_MARKING
		bool m_isAlive;
#endif
		typedef T LocalValueType;

		T m_value;

		TransientLocal();
	};

	template<class T>
	struct NoInitPermanentLocal
	{
		typedef T LocalValueType;

		T m_value;
	};

	template<class T>
	struct InitPermanentLocal
	{
		typedef T LocalValueType;

		T m_value;

		InitPermanentLocal();
	};

	template<class T>
	struct TMaybeAnchoredManagedPtr
#if CLARITY_MEMORY_RELOCATION != 0
		: public ::ClarityInternal::TypeDef<typename ::CLRUtil::TAnchoredManagedPtr<T>::Type>

#else
		: public ::ClarityInternal::TypeDef<T*>
#endif
	{
	};

	template<class T>
	struct TValueObjectType
		: public ClarityInternal::TypeDef<
		typename CLRPrivate::TValueObjectTypeResolver_ByTraits<
		T,
		CLRTI::TypeProtoTraits<T>::IsValueType,
		CLRTI::TypeProtoTraits<T>::IsArray,
		CLRTI::TypeProtoTraits<T>::IsReferenceArray
		>::Type
		>
	{
	};

	template<class T>
	struct TRefValue
		: public ::ClarityInternal::TypeDef<
				typename ::CLRPrivate::TRefTypeValueResolver_ByTraits<
				::CLRTI::TypeProtoTraits<T>::IsValueType,
				::CLRTI::TypeProtoTraits<T>::IsArray,
				::CLRTI::TypeProtoTraits<T>::IsReferenceArray,
				T
			>::Type
		>
	{
	};


	template<class T>
	struct TValValue
		: public ::ClarityInternal::TypeDef <typename ::CLRPrivate::TValValueResolver_IsValueType<T, ::CLRTI::TypeProtoTraits<T>::IsValueType>::Type >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tByte >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::U8 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tSByte >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::S8 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tInt16 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::S16 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tUInt16 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::U16 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tInt32 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::S32 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tUInt32 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::U32 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tInt64 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::S64 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tUInt64 >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::U64 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tBoolean >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::Bool >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tSingle >
		: public ::ClarityInternal::TypeDef< ::CLRTypes::F32 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tDouble >
		: public ClarityInternal::TypeDef< CLRTypes::F64 >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tIntPtr >
		: public ClarityInternal::TypeDef< CLRTypes::TypeTaggedNumber< ::CLRX::NtSystem::tIntPtr, ::CLRTypes::IntPtr > >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tUIntPtr >
		: public ClarityInternal::TypeDef< CLRTypes::TypeTaggedNumber< CLRX::NtSystem::tUIntPtr, CLRTypes::UIntPtr > >
	{
	};

	template<>
	struct TValValue< ::CLRX::NtSystem::tChar >
		: public ::ClarityInternal::TypeDef< CLRTypes::TypeTaggedNumber< CLRX::NtSystem::tChar, CLRTypes::U16 > >
	{
	};



	template<class T> struct IsNumberTypeTagged { };

	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tBoolean> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tByte> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tSByte> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tInt16> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tUInt16> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tInt32> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tUInt32> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tInt64> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tUInt64> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tChar> { enum { Value = 1 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tIntPtr> { enum { Value = 1 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tUIntPtr> { enum { Value = 1 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tSingle> { enum { Value = 0 }; };
	template<> struct IsNumberTypeTagged<CLRX::NtSystem::tDouble> { enum { Value = 0 }; };

	template<class T>
	typename CLRUtil::TSimpleRef<T>::Type ParamThis(T *bThis);

	template<class T>
	typename CLRVM::TRefValue<T>::Type AllocObject(const CLRExec::Frame &frame);

	template<class T>
	typename CLRVM::TValValue< CLRCore::SZArray<T> >::Type AllocSZArray(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT nElements) { CLARITY_NOTIMPLEMENTED; }

	template<class T>
	struct TNumberStorageType { };

	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tChar > : public ::ClarityInternal::TypeDef< CLRTypes::U16 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tSByte > : public ::ClarityInternal::TypeDef< CLRTypes::S8 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tInt16 > : public ::ClarityInternal::TypeDef< CLRTypes::S16 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tInt32 > : public ::ClarityInternal::TypeDef< CLRTypes::S32 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tInt64 > : public ::ClarityInternal::TypeDef< CLRTypes::S64 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tByte > : public ::ClarityInternal::TypeDef< CLRTypes::U8 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tUInt16 > : public ::ClarityInternal::TypeDef< CLRTypes::U16 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tUInt32 > : public ::ClarityInternal::TypeDef< CLRTypes::U32 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tUInt64 > : public ::ClarityInternal::TypeDef< CLRTypes::U64 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tSingle > : public ::ClarityInternal::TypeDef< CLRTypes::F32 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tDouble > : public ::ClarityInternal::TypeDef< CLRTypes::F64 > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tIntPtr > : public ::ClarityInternal::TypeDef< CLRTypes::IntPtr > { };
	template<>
	struct TNumberStorageType< ::CLRX::NtSystem::tUIntPtr > : public ::ClarityInternal::TypeDef< CLRTypes::UIntPtr > { };

	struct ELocalType
		: public ::ClarityInternal::NoCreate
	{
		enum Enum
		{
			Argument,
			Local,
			Temporary
		};
	};

	// Ref local
	template<int TLocalType, class T>
	struct TRefLocal
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TRefLocal<ELocalType::Argument, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TRefValue<T>::Type > >
	{
	};

	template<class T>
	struct TRefLocal<ELocalType::Local, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TRefValue<T>::Type > >
	{
	};

	template<class T>
	struct TRefLocal<ELocalType::Temporary, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TRefValue<T>::Type > >
	{
	};

	template<class T>
	struct TStaticTokenLocal
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRUtil::TSimpleRef< typename ::CLRTI::TypeTraits<T>::StaticType >::Type > >
	{
	};

	// Anchored managed ptr local
	template<int TLocalType, class T>
	struct TAnchoredManagedPtrLocal
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TAnchoredManagedPtrLocal<ELocalType::Argument, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TAnchoredManagedPtrLocal<ELocalType::Local, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TAnchoredManagedPtrLocal<ELocalType::Temporary, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRUtil::TAnchoredManagedPtr<T>::Type > >
	{
	};

	// Local managed ptr local
	template<int TLocalType, class T>
	struct TLocalManagedPtrLocal
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TLocalManagedPtrLocal<ELocalType::Argument, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TLocalManagedPtrLocal<ELocalType::Local, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TLocalManagedPtrLocal<ELocalType::Temporary, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRUtil::TLocalManagedPtr<T>::Type > >
	{
	};

	// Maybe anchored managed ptr local
	template<int TLocalType, class T>
	struct TMaybeAnchoredManagedPtrLocal
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TMaybeAnchoredManagedPtrLocal<ELocalType::Argument, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TMaybeAnchoredManagedPtrLocal<ELocalType::Local, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
	{
	};

	template<class T>
	struct TMaybeAnchoredManagedPtrLocal<ELocalType::Temporary, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TMaybeAnchoredManagedPtr<T>::Type > >
	{
	};

	// Value local
	template<int TLocalType, class T>
	struct TValLocal
		: public ::ClarityInternal::NoCreate
	{
	};

	template<class T>
	struct TValLocal<ELocalType::Argument, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::NoInitPermanentLocal< typename ::CLRVM::TValValue<T>::Type > >
	{
	};

	template<class T>
	struct TValLocal<ELocalType::Local, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::InitPermanentLocal< typename ::CLRVM::TValValue<T>::Type > >
	{
	};

	template<class T>
	struct TValLocal<ELocalType::Temporary, T>
		: public ::ClarityInternal::TypeDef< ::CLRVM::TransientLocal< typename ::CLRVM::TValValue<T>::Type > >
	{
	};

	// Local tracers
	template<int TLocalType, class T>
	struct LocalTracerFuncs
	{
		static void TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<TLocalType, T>::Type &ref);
		static void TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<TLocalType, T>::Type &ref);
		static void TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<TLocalType, T>::Type &ref);
		static void TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<TLocalType, T>::Type &ref);

	private:
		LocalTracerFuncs();
	};

#if CLARITY_PRECISE_TEMPORARY_MARKING
	// Precise local tracers
	template<class T>
	struct LocalTracerFuncs<ELocalType::Temporary, T>
	{
		static void TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<ELocalType::Temporary, T>::Type &ref);
		static void TraceMaybeAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<ELocalType::Temporary, T>::Type &ref);
		static void TraceValLocal(::CLRExec::IRefVisitor &visitor, typename TValLocal<ELocalType::Temporary, T>::Type &ref);
		static void TraceRefLocal(::CLRExec::IRefVisitor &visitor, typename TRefLocal<ELocalType::Temporary, T>::Type &ref);

	private:
		LocalTracerFuncs();
	};
#endif

	// Value tracers
	template<class T>
	struct TracerFuncs
	{
		static void TraceAnchoredManagedPtr(CLRExec::IRefVisitor &visitor, typename CLRUtil::TAnchoredManagedPtr<T>::Type &ref);
		static void TraceMaybeAnchoredManagedPtr(CLRExec::IRefVisitor &visitor, typename CLRVM::TMaybeAnchoredManagedPtr<T>::Type &ref);
		static void TraceVal(CLRExec::IRefVisitor &visitor, typename CLRVM::TValValue<T>::Type &ref);
		static void TraceRef(CLRExec::IRefVisitor &visitor, typename CLRVM::TRefValue<T>::Type &ref);
	};

	// Static token tracer
	template<class T>
	void TraceStaticToken(CLRExec::IRefVisitor &visitor, typename CLRVM::InitPermanentLocal< typename CLRUtil::TSimpleRef< typename CLRTI::TypeTraits<T>::StaticType >::Type > &ref);

	template<class T>
	typename CLRVM::TRefValue<T>::Type AllocObject(const CLRExec::Frame &frame);

	template<class TLocalType>
	typename TLocalType::LocalValueType &LivenVReg(TLocalType &local);

	template<class TLocalType>
	typename TLocalType::LocalValueType &KillAndReturnVReg(TLocalType &local);

	template<class TLocalType>
	void KillVReg(TLocalType &local);

	template<class TLocalType>
	typename TLocalType::LocalValueType &VRegValue(TLocalType &local);

	template<class TA, class TB>
	struct ReferenceEqualityComparer
		: public ::CLRPrivate::ReferenceEqualityComparer_ByTraits<
		::CLRTI::TypeProtoTraits<TA>::IsInterface,
		::CLRTI::TypeProtoTraits<TB>::IsInterface,
		::ClarityInternal::AreTypesSame<TA, TB>::Value,
		TA, TB
		>
	{
	};

	template<class T>
	bool IsNumberZero(const T &a);

	template<class T>
	bool IsNull(const typename ::CLRVM::TRefValue<T>::Type &a);

	template<class T>
	typename CLRVM::TRefValue<T>::Type StringConstant(const CLRExec::Frame &frame, bool isPacked, CLRTypes::SizeT length, CLRTypes::S32 hash, const char *value);

	template<class T>
	class Field
	{
	public:
		const typename CLRVM::TValValue<T>::Type &Value() const;
		void Set(const typename CLRVM::TValValue<T>::Type &value);

		void VisitReferences(CLRExec::IRefVisitor &visitor);

	private:
		typename CLRVM::TValValue<T>::Type m_value;
	};

	template<class TSource, class TDest>
	class DynamicCaster
	{
	public:
		static typename ::CLRVM::TRefValue<TDest>::Type Cast(const typename ::CLRVM::TRefValue<TSource>::Type &src);
	};

	template<class T>
	void InitStaticToken(const CLRExec::Frame &frame, typename CLRUtil::TSimpleRef< typename CLRTI::TypeTraits<T>::StaticType >::Type &staticToken);

	template<class T>
	class ArrayCreator
		: public ClarityInternal::NoCreate
	{
	public:
		static typename CLRVM::TValValue<T>::Type Create(const CLRExec::Frame &frame, CLRVM::TValValue<CLRX::NtSystem::tIntPtr>::Type nElements) { CLARITY_NOTIMPLEMENTED; }
		static typename CLRVM::TValValue<T>::Type Create(const CLRExec::Frame &frame, CLRTypes::S32 nElements) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	class SZArrayLoader
		: public ClarityInternal::NoCreate
	{
	public:
		static typename CLRVM::TValValue<typename T::TSubscriptType>::Type Load(const CLRExec::Frame &frame, const typename CLRVM::TValValue<T>::Type &arrayRef, CLRVM::TValValue<CLRX::NtSystem::tIntPtr>::Type index) { CLARITY_NOTIMPLEMENTED; }
		static typename CLRVM::TValValue<typename T::TSubscriptType>::Type Load(const CLRExec::Frame &frame, const typename CLRVM::TValValue<T>::Type &arrayRef, CLRTypes::S32 index) { CLARITY_NOTIMPLEMENTED; }
	};

	template<class T>
	class SZArrayStorer
		: public ClarityInternal::NoCreate
	{
	public:
		static void Store(const CLRExec::Frame &frame, const typename CLRVM::TValValue<T>::Type &arrayRef, CLRVM::TValValue<CLRX::NtSystem::tIntPtr>::Type index, const typename CLRVM::TValValue<typename T::TSubscriptType>::Type &value);
		static void Store(const CLRExec::Frame &frame, const typename CLRVM::TValValue<T>::Type &arrayRef, CLRTypes::S32 index, const typename CLRVM::TValValue<typename T::TSubscriptType>::Type &value);
	};

	template<class T>
	class ArithOps
		: public ClarityInternal::NoCreate
	{
	private:
		typedef typename CLRVM::TValValue<T>::Type TValueType;

	public:
		static TValueType Add(const TValueType &a, const TValueType &b);
		static TValueType Subtract(const TValueType &a, const TValueType &b);
		static TValueType Multiply(const TValueType &a, const TValueType &b);
		static TValueType Divide(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
		static TValueType Modulo(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
		static TValueType BitwiseAnd(const TValueType &a, const TValueType &b);
		static TValueType BitwiseOr(const TValueType &a, const TValueType &b);
		static TValueType BitwiseXor(const TValueType &a, const TValueType &b);

		static TValueType AddOvf(const TValueType &a, const TValueType &b);
		static TValueType AddOvfUn(const TValueType &a, const TValueType &b);
		static TValueType SubtractOvf(const TValueType &a, const TValueType &b);
		static TValueType SubtractOvfUn(const TValueType &a, const TValueType &b);
		static TValueType MultiplyOvf(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
		static TValueType MultiplyOvfUn(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
		static TValueType DivideUn(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
		static TValueType ModuloUn(const CLRExec::Frame &frame, const TValueType &a, const TValueType &b);
	};
}

#include "ClarityNumberConversions.h"

///////////////////////////////////////////////////////////////////////////////
// ****************************** EXPORT-DEPENDENT DEFS ******************************
#include "tSystem/tArray.Def.h"
#include "tSystem/tObject.Def.h"

namespace CLRCore
{
	struct RefArrayContainerBase
		: public CLRX::NtSystem::tArray
	{
	};

	struct ValueArrayContainerBase
		: public CLRX::NtSystem::tArray
	{
	};

	template<class T>
	struct ValueArrayContainer
		: public CLRCore::ValueArrayContainerBase
	{
	};

	template<class T>
	struct RefArrayContainer
		: public CLRCore::RefArrayContainerBase
	{
	};
}

namespace CLRVM
{
	inline void Throw(const ::CLRExec::Frame &frame, CLRVM::TValValue<CLRX::NtSystem::tObject>::Type obj) { CLARITY_NOTIMPLEMENTED; }
}


///////////////////////////////////////////////////////////////////////////////
// ****************************** INLINE CODE ******************************
#include "ClarityExec.h"

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::RefTracer<0, 0, 0, 0, T>::Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef<T>::Type &ref)
{
	ref = CLRPrivate::SimpleRefVisitor<T>::VisitObject(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::RefTracer<0, 1, 0, 0, T>::Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef<T>::Type &ref)
{
	ref = CLRPrivate::SimpleRefVisitor<T>::VisitInterface(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::RefTracer<0, 0, 1, 0, T>::Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::TSimpleRef< CLRCore::ValueArrayContainer<typename T::TSubscriptType> >::Type &ref)
{
	ref = CLRPrivate::SimpleRefVisitor< CLRCore::ValueArrayContainer<typename T::TSubscriptType> >::VisitObject(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::RefTracer<0, 0, 1, 1, T>::Trace(CLRExec::IRefVisitor &visitor, typename CLRUtil::RefArrayReference< T > &ref)
{
	CLARITY_NOTIMPLEMENTED;
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::RefTracer<1, 0, 0, 0, T>::Trace(CLRExec::IRefVisitor &visitor, const typename CLRVM::TRefValue<T>::Type &ref)
{
	CLRUtil::RetargetSimpleRef(ref);
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::ValTracer<1, 0, 0, 0, 0, T>::Trace(::CLRExec::IRefVisitor &visitor, const typename ::CLRVM::TValValue<T>::Type &ref)
{
	// Referenceless value type
}

template<class T>
CLARITY_FORCEINLINE void CLRPrivate::ValTracer<1, 0, 0, 0, 1, T>::Trace(::CLRExec::IRefVisitor &visitor, typename ::CLRVM::TValValue<T>::Type &ref)
{
	ref.VisitReferences(visitor);
}

#if CLARITY_PRECISE_TEMPORARY_MARKING

template<class T>
CLARITY_FORCEINLINE::CLRVM::TransientLocal<T>::TransientLocal()
	: m_isAlive(false)
{
}

#else

template<class T>
CLARITY_FORCEINLINE::CLRVM::TransientLocal<T>::TransientLocal()
{
	memset(&this->m_value, 0, sizeof(this->m_value));
};

#endif

template<class T>
CLARITY_FORCEINLINE::CLRVM::InitPermanentLocal<T>::InitPermanentLocal()
{
	memset(&this->m_value, 0, sizeof(this->m_value));
};

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &(::CLRVM::LivenVReg)(TLocalType &local)
{
	return ::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::Liven(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &(::CLRVM::KillAndReturnVReg)(TLocalType &local)
{
	return ::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::KillAndReturn(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE void ::CLRVM::KillVReg(TLocalType &local)
{
	::CLRPrivate::VRegOps_ByValueType<typename TLocalType::LocalValueType>::Kill(local);
}

template<class TLocalType>
CLARITY_FORCEINLINE typename TLocalType::LocalValueType &::CLRVM::VRegValue(TLocalType &local)
{
	return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::Liven(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
	return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::Liven(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
	local.m_isAlive = true;
#endif
	return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE void ::CLRPrivate::VRegOps_ByValueType<TValueType>::Kill(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
}

template<class TValueType>
CLARITY_FORCEINLINE void ::CLRPrivate::VRegOps_ByValueType<TValueType>::Kill(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
	local.m_isAlive = false;
#endif
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::KillAndReturn(::CLRVM::NoInitPermanentLocal<TValueType> &local)
{
	return local.m_value;
}

template<class TValueType>
CLARITY_FORCEINLINE TValueType &::CLRPrivate::VRegOps_ByValueType<TValueType>::KillAndReturn(::CLRVM::TransientLocal<TValueType> &local)
{
#if CLARITY_PRECISE_TEMPORARY_MARKING
	local.m_isAlive = false;
#endif
	return local.m_value;
}


template<class T>
CLARITY_FORCEINLINE typename CLRUtil::TSimpleRef<T>::Type CLRVM::ParamThis(T *bThis)
{
	return typename CLRUtil::TSimpleRef<T>::Type(bThis);
}

// Local tracers
template<int TLocalType, class T>
CLARITY_FORCEINLINE void CLRVM::LocalTracerFuncs<TLocalType, T>::TraceAnchoredManagedPtrLocal(CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal<TLocalType, T>::Type &ref)
{
	::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void CLRVM::LocalTracerFuncs<TLocalType, T>::TraceMaybeAnchoredManagedPtrLocal(CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal<TLocalType, T>::Type &ref)
{
	CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void CLRVM::LocalTracerFuncs<TLocalType, T>::TraceValLocal(CLRExec::IRefVisitor &visitor, typename TValLocal<TLocalType, T>::Type &ref)
{
	::CLRVM::TracerFuncs<T>::TraceVal(visitor, ref.m_value);
}

template<int TLocalType, class T>
CLARITY_FORCEINLINE void CLRVM::LocalTracerFuncs<TLocalType, T>::TraceRefLocal(CLRExec::IRefVisitor &visitor, typename TRefLocal<TLocalType, T>::Type &ref)
{
	::CLRVM::TracerFuncs<T>::TraceRef(visitor, ref.m_value);
}

template<class T>
CLARITY_FORCEINLINE void CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(CLRExec::IRefVisitor &visitor, typename CLRUtil::TAnchoredManagedPtr<T>::Type &ref)
{
	ref.Visit(visitor);
}

template<class T>
CLARITY_FORCEINLINE void ::CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(CLRExec::IRefVisitor &visitor, typename CLRVM::TMaybeAnchoredManagedPtr<T>::Type &ref)
{
#if CLARITY_MEMORY_RELOCATION != 0
	ref.Visit(visitor);
#endif
}

template<class T>
CLARITY_FORCEINLINE void CLRVM::TracerFuncs<T>::TraceVal(CLRExec::IRefVisitor &visitor, typename CLRVM::TValValue<T>::Type &ref)
{
	::CLRPrivate::ValTracer<
		CLRTI::TypeProtoTraits<T>::IsValueType,
		CLRTI::TypeProtoTraits<T>::IsInterface,
		CLRTI::TypeProtoTraits<T>::IsArray,
		CLRTI::TypeProtoTraits<T>::IsReferenceArray,
		CLRTI::TypeTraits<T>::IsValueTraceable,
		T
	>::Trace(visitor, ref);
}

template<class T>
CLARITY_FORCEINLINE void CLRVM::TracerFuncs<T>::TraceRef(CLRExec::IRefVisitor &visitor, typename CLRVM::TRefValue<T>::Type &ref)
{
	CLRPrivate::RefTracer<
		CLRTI::TypeProtoTraits<T>::IsValueType,
		CLRTI::TypeProtoTraits<T>::IsInterface,
		CLRTI::TypeProtoTraits<T>::IsArray,
		CLRTI::TypeProtoTraits<T>::IsReferenceArray,
		T
	>::Trace(visitor, ref);
}


#if CLARITY_PRECISE_TEMPORARY_MARKING

// Precise temporary marking tracers
template<class T>
inline void ::CLRVM::LocalTracerFuncs< ::CLRVM::ELocalType::Temporary, T >::TraceAnchoredManagedPtrLocal(::CLRExec::IRefVisitor &visitor, typename TAnchoredManagedPtrLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
	if (ref.m_isAlive)
		::CLRVM::TracerFuncs<T>::TraceAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs< CLRVM::ELocalType::Temporary, T >::TraceMaybeAnchoredManagedPtrLocal(CLRExec::IRefVisitor &visitor, typename TMaybeAnchoredManagedPtrLocal< CLRVM::ELocalType::Temporary, T >::Type &ref)
{
	if (ref.m_isAlive)
		CLRVM::TracerFuncs<T>::TraceMaybeAnchoredManagedPtr(visitor, ref.m_value);
}

template<class T>
inline void CLRVM::LocalTracerFuncs< CLRVM::ELocalType::Temporary, T >::TraceValLocal(CLRExec::IRefVisitor &visitor, typename TValLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
	if (ref.m_isAlive)
		CLRVM::TracerFuncs<T>::TraceVal(visitor, ref.m_value);
}

template<class T>
inline void ::CLRVM::LocalTracerFuncs< CLRVM::ELocalType::Temporary, T >::TraceRefLocal(CLRExec::IRefVisitor &visitor, typename TRefLocal< ::CLRVM::ELocalType::Temporary, T >::Type &ref)
{
	if (ref.m_isAlive)
		CLRVM::TracerFuncs<T>::TraceRef(visitor, ref.m_value);
}

#endif

template<class T>
inline void CLRVM::TraceStaticToken(CLRExec::IRefVisitor &visitor, typename CLRVM::InitPermanentLocal< typename CLRUtil::TSimpleRef< typename CLRTI::TypeTraits<T>::StaticType >::Type > &ref)
{
	ref.m_value = CLRPrivate::SimpleRefVisitor<typename CLRTI::TypeTraits<T>::StaticType>::VisitObject(visitor, ref.m_value);
}


template<class T>
inline typename CLRVM::TRefValue<T>::Type CLRVM::AllocObject(const ::CLRExec::Frame &frame)
{
	return typename CLRVM::TRefValue<T>::Type(frame.GetObjectManager()->AllocObject<T>(frame));
}

template<class T>
inline typename CLRVM::TValValue< CLRCore::SZArray<T> >::Type AllocSZArray(const ::CLRExec::Frame &frame, ::CLRTypes::SizeT nElements)
{
	CLRCore::SZArray<T>::ComputeSize(frame, nElements);
}

template<class T>
CLARITY_FORCEINLINE bool CompareEqual(const T &a, const T &b)
{
	return a == b;
}

template<class T>
CLARITY_FORCEINLINE bool CLRVM::IsNumberZero(const T &v)
{
	return v == T(0);
}

template<class T>
CLARITY_FORCEINLINE bool CLRVM::IsNull(const typename ::CLRVM::TRefValue<T>::Type &v)
{
#if CLARITY_USE_STRICT_REFS != 0
	return v.IsNull();
#else
	return v == CLARITY_NULLPTR;
#endif
}

template<class T>
inline typename CLRVM::TRefValue<T>::Type CLRVM::StringConstant(const CLRExec::Frame &frame, bool isPacked, CLRTypes::SizeT length, CLRTypes::S32 hash, const char *value)
{
	T *strObj = static_cast<T*>(frame.GetObjectManager()->GetStringConstant(frame, isPacked, length, hash, value));
	return typename CLRVM::TRefValue<T>::Type(strObj);
}


template<class T>
CLARITY_FORCEINLINE const typename CLRVM::TValValue<T>::Type &CLRVM::Field<T>::Value() const
{
	return this->m_value;
}

template<class T>
inline void CLRVM::Field<T>::Set(const typename CLRVM::TValValue<T>::Type &value)
{
	this->m_value = value;
#if CLARITY_INCREMENTAL_GC
#error "Not implemented"
#endif
}

template<class T>
CLARITY_FORCEINLINE void CLRVM::Field<T>::VisitReferences(CLRExec::IRefVisitor &visitor)
{
	TracerFuncs<T>::TraceVal(visitor, this->m_value);
}

template<class T>
inline void CLRVM::InitStaticToken(const CLRExec::Frame &frame, typename CLRUtil::TSimpleRef< typename CLRTI::TypeTraits<T>::StaticType >::Type &staticToken)
{
	::CLRCore::IObjectManager *objManager = frame.GetObjectManager();

	::CLRCore::GCObject *staticInstance = objManager->GetStaticClass(frame, T::bStaticCacheLocator, T::RttiQuery);
	typename ::CLRTI::TypeTraits<T>::StaticType *staticContainer = static_cast<typename ::CLRTI::TypeTraits<T>::StaticType*>(staticInstance);
	staticToken = typename ::CLRUtil::TSimpleRef< typename ::CLRTI::TypeTraits<T>::StaticType >::Type(staticContainer);
}

// CLARITYTODO: Test comparisons of interfaces to be compared to ref array references?

template<int TTypesAreSame, class TA, class TB>
CLARITY_FORCEINLINE bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<0, 0, TTypesAreSame, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Object-object
	const CLRCore::GCObject *refA = CLRVM::RefRootResolver<TA>::Resolve(a);
	const CLRCore::GCObject *refB = CLRVM::RefRootResolver<TB>::Resolve(b);

	return refA == refB;
}

template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<1, 0, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Interface-object
	const typename ::CLRVM::TValueObjectType<TA>::Type *ifcA = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TA>::Type *>(a);
	const CLRCore::GCObject *refB = CLRVM::RefRootResolver<TB>::Resolve(b);

	if (ifcA == CLARITY_NULLPTR)
		return refB == CLARITY_NULLPTR;
	return ifcA->GetRootGCObject() == refB;
}

template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<0, 1, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Object-interface
	const CLRCore::GCObject *refA = CLRVM::RefRootResolver<TA>::Resolve(a);
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcB = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TB>::Type *>(b);

	if (ifcB == CLARITY_NULLPTR)
		return refA == CLARITY_NULLPTR;
	return ifcB->GetRootGCObject() == refA;
}


template<class TA, class TB>
inline bool ::CLRPrivate::ReferenceEqualityComparer_ByTraits<1, 1, 0, TA, TB>::AreEqual(const typename ::CLRVM::TRefValue<TA>::Type &a, const typename ::CLRVM::TRefValue<TB>::Type &b)
{
	// Interface-interface, different types
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcA = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TA>::Type *>(a);
	typename ::CLRVM::TValueObjectType<TA>::Type *ifcB = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TB>::Type *>(b);

	if (ifcA == CLARITY_NULLPTR)
		return ifcB == CLARITY_NULLPTR;
	if (ifcB == CLARITY_NULLPTR)
		return false;

	return ifcB->GetRootGCObject() == ifcA->GetRootGCObject();
}

#include <new>

template<class T>
inline T *::CLRCore::IObjectManager::AllocObject(const ::CLRExec::Frame &frame)
{
    T *obj = static_cast<T*>(this->MemAlloc(frame, sizeof(T), true));
    new (obj) T();
    return obj;
}

///////////////////////////////////////////////////////////////////////////////

template<class T>
CLARITY_FORCEINLINE const typename ::CLRVM::TValValue<T>::Type &(::CLRPrivate::DelegateVarianceConverter_ByTraits<1, 1, T, T>::Convert)(const typename ::CLRVM::TValValue<T>::Type &value)
{
	return value;
}

// Type sameness is enforced for delegates - They can not be variant even if passive conversions are possible
template<class TSource, class TDest>
CLARITY_FORCEINLINE const typename ::CLRVM::TValValue<TSource>::Type &(::CLRPrivate::DelegateVarianceConverter_ByTraits<0, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TValValue<TSource>::Type &value)
{
	return ::CLRUtil::PassiveReferenceConverter<TSource, TDest>::Convert(value);
}

template<class TSource, class TDest>
typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_RefArrayRefArray_ByTraits<0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Passive convert incompatible reference array references
	CLARITY_NOTIMPLEMENTED;
}

template<class T>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<T>::Type(::CLRPrivate::PassiveReferenceConverter_RefArrayRefArray_ByTraits<1, T, T>::Convert)(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return ref;
}

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_ObjObj_ByTraits<0, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Passive conversion of non-ref-array ref to non-ref-array ref
	TDest *destPtr = CLRVM::RefRootResolver<TSource>::Resolve(ref);
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_ObjObj_ByTraits<1, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Passive conversion of an array to object type
	CLARITY_NOTIMPLEMENTED;
}

template<class T>
CLARITY_FORCEINLINE T *CLRPrivate::SimpleRefVisitor<T>::VisitObject(CLRExec::IRefVisitor &visitor, T *ref)
{
	// Classes implement multiple ref targets.  Disambiguate to the root one.
	return static_cast<T*>(static_cast< ::CLRCore::GCObject* >(visitor.TouchReference(static_cast< ::CLRCore::GCObject* >(ref))));
}

template<class T>
CLARITY_FORCEINLINE T *CLRPrivate::SimpleRefVisitor<T>::VisitInterface(CLRExec::IRefVisitor &visitor, T *ref)
{
	// Interfaces only implement one ref target.
	return static_cast<T*>(visitor.TouchReference(ref));
}

#if CLARITY_USE_STRICT_REFS != 0

template<class T>
CLARITY_FORCEINLINE CLRUtil::StrictRef<T> CLRPrivate::SimpleRefVisitor<T>::VisitObject(CLRExec::IRefVisitor &visitor, const CLRUtil::StrictRef<T> &ref)
{
	return CLRUtil::StrictRef<T>(CLRPrivate::SimpleRefVisitor<T>::VisitObject(visitor, ref.GetPtr()));
}

template<class T>
CLARITY_FORCEINLINE CLRUtil::StrictRef<T> CLRPrivate::SimpleRefVisitor<T>::VisitInterface(CLRExec::IRefVisitor &visitor, const CLRUtil::StrictRef<T> &ref)
{
	return CLRUtil::StrictRef<T>(CLRPrivate::SimpleRefVisitor<T>::VisitInterface(visitor, ref.GetPtr()));
}

#else

#endif


#if CLARITY_USE_STRICT_REFS != 0

template<class T>
CLARITY_FORCEINLINE::CLRUtil::StrictRef<T>::StrictRef()
{
}

template<class T>
CLARITY_FORCEINLINE::CLRUtil::StrictRef<T>::StrictRef(const StrictRef &other)
	: m_ptr(other.m_ptr)
{
}

template<class T>
CLARITY_FORCEINLINE::CLRUtil::StrictRef<T>::StrictRef(T *ptr)
	: m_ptr(ptr)
{
}

template<class T>
CLARITY_FORCEINLINE::CLRUtil::StrictRef<T> &::CLRUtil::StrictRef<T>::operator =(const ::CLRUtil::StrictRef<T>& other)
{
	this->m_ptr = other.m_ptr;
	return *this;
}

template<class T>
CLARITY_FORCEINLINE bool ::CLRUtil::StrictRef<T>::operator ==(const ::CLRUtil::StrictRef<T>& other) const
{
	return this->m_ptr == other.m_ptr;
}

template<class T>
CLARITY_FORCEINLINE T *::CLRUtil::StrictRef<T>::operator ->() const
{
	return m_ptr;
}

template<class T>
CLARITY_FORCEINLINE CLRUtil::Boxed<T> *::CLRPrivate::DelegateTargetConverter_ByTraits<1, 0, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast< ::CLRUtil::Boxed<T>* >(static_cast< ::CLRCore::GCObject* >(dgTarget));
}

template<class T>
CLARITY_FORCEINLINE CLRUtil::TDGTarget CLRPrivate::DelegateTargetConverter_ByTraits<1, 0, T>::ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return static_cast< ::CLRCore::GCObject* >(ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		);
}

template<class T>
CLARITY_FORCEINLINE T *CLRPrivate::DelegateTargetConverter_ByTraits<0, 0, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast<T*>(static_cast< ::CLRCore::GCObject* >(dgTarget));
}

template<class T>
CLARITY_FORCEINLINE CLRUtil::TDGTarget CLRPrivate::DelegateTargetConverter_ByTraits<0, 0, T>::ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return static_cast< ::CLRCore::GCObject* >(ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		);
}

template<class T>
CLARITY_FORCEINLINE T *CLRPrivate::DelegateTargetConverter_ByTraits<0, 1, T>::FromTarget(::CLRUtil::TDGTarget dgTarget)
{
	return static_cast<T*>(dgTarget);
}

template<class T>
CLARITY_FORCEINLINE CLRUtil::TDGTarget CLRPrivate::DelegateTargetConverter_ByTraits<0, 1, T>::ToTarget(const typename ::CLRVM::TRefValue<T>::Type &ref)
{
	return ref
#if CLARITY_USE_STRICT_REFS != 0
		.GetPtr()
#endif
		;
}



template<class TSource, class TDest>
CLARITY_FORCEINLINE typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_ByTraits<0, 1, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Object to interface conversion
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr = srcPtr;
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
inline typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_ByTraits<1, 0, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Interface to object conversion.  This is ONLY valid for conversion to System.Object
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr = (srcPtr == CLARITY_NULLPTR) ? CLARITY_NULLPTR : srcPtr->GetRootObject();
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class TSource, class TDest>
inline typename ::CLRVM::TRefValue<TDest>::Type(::CLRPrivate::PassiveReferenceConverter_ByTraits<1, 1, TSource, TDest>::Convert)(const typename ::CLRVM::TRefValue<TSource>::Type &ref)
{
	// Interface to interface conversion
	typename ::CLRVM::TValueObjectType<TSource>::Type *srcPtr = ::CLRUtil::RefToPtr<typename ::CLRVM::TValueObjectType<TSource>::Type>(ref);
	typename ::CLRVM::TValueObjectType<TDest>::Type *destPtr;
	if (srcPtr == CLARITY_NULLPTR)
		destPtr = CLARITY_NULLPTR;
	else
		srcPtr->iPassiveConvertInterface(destPtr);
	return typename ::CLRVM::TRefValue<TDest>::Type(destPtr);
}

template<class T>
CLARITY_FORCEINLINE bool(::CLRUtil::StrictRef<T>::IsNull)() const
{
	return this->m_ptr == CLARITY_NULLPTR;
}

template<class T>
CLARITY_FORCEINLINE T *(::CLRUtil::StrictRef<T>::GetPtr)() const
{
	return this->m_ptr;
}

#endif

template<class TSource, class TDest>
CLARITY_FORCEINLINE typename CLRVM::TValValue<TDest>::Type CLRUtil::PassiveValueConverter<TSource, TDest>::Convert(typename CLRVM::TValValue<TSource>::Type val)
{
	return CLRUtil::PassiveValueConversionWriter<typename CLRVM::TValValue<TDest>::Type>::FromMid(CLRUtil::PassiveValueConversionLoader<typename CLRVM::TValValue<TSource>::Type>::ToMid(val));
}

template<class T>
CLARITY_FORCEINLINE typename CLRVM::TRefValue<T>::Type CLRUtil::NullReference()
{
	return typename CLRVM::TRefValue<T>::Type(static_cast<T*>(CLARITY_NULLPTR));
}


template<class TSource, class TMid>
CLARITY_FORCEINLINE TMid CLRUtil::PassiveValueSimpleConversionLoader<TSource, TMid>::ToMid(TSource src)
{
	return TMid(src);
}

CLARITY_FORCEINLINE CLRTypes::S32 CLRUtil::PassiveValueConversionLoader< ::CLRTypes::Bool >::ToMid(CLRTypes::Bool src)
{
	return (src == false) ? (::CLRTypes::S32(1)) : (::CLRTypes::S32(0));
};

template<class TMid, class TDest>
CLARITY_FORCEINLINE TDest CLRUtil::PassiveValueSimpleConversionWriter<TMid, TDest>::FromMid(TMid mid)
{
	return TDest(mid);
};

CLARITY_FORCEINLINE CLRTypes::Bool CLRUtil::PassiveValueConversionWriter< ::CLRTypes::Bool >::FromMid(CLRTypes::S32 mid)
{
	return ::CLRTypes::Bool(mid != 0);
}

template<class T>
inline void CLRUtil::AnchoredManagedPtr<T>::Visit(CLRExec::IRefVisitor &refVisitor)
{
	::CLRCore::RefTarget *ref = this->m_object;
	if (ref != CLARITY_NULLPTR)
	{
		::CLRTypes::PtrDiffT valueOffset = reinterpret_cast<const ::CLRTypes::U8*>(this->m_value) - reinterpret_cast<const ::CLRTypes::U8*>(ref);
		ref = refVisitor.TouchReference(ref);
		this->m_object = ref;
		this->m_value = reinterpret_cast<T*>(reinterpret_cast< ::CLRTypes::U8* >(ref) + valueOffset);
	}
}

#if CLARITY_USE_STRICT_REFS != 0

template<class T>
CLARITY_FORCEINLINE T *CLRUtil::SimpleRefToPtr(const StrictRef<T> &ref)
{
	return ref.GetPtr();
}

#else

template<class T>
CLARITY_FORCEINLINE T *::CLRUtil::SimpleRefToPtr(T *ref)
{
	return ref;
}

#endif

#endif
