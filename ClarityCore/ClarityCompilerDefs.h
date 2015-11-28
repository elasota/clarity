#ifndef __CLARITY_COMPILER_DEFS_H__
#define __CLARITY_COMPILER_DEFS_H__

#include "ClarityConfig.h"

#ifdef _MSC_VER
	#define CLARITY_FORCEINLINE __forceinline
	#if CLARITY_CPP11 != 0
		#define CLARITY_OVERRIDE override
		#define CLARITY_FINAL sealed
		#define CLARITY_NULLPTR nullptr
	#else
		#define CLARITY_OVERRIDE
		#define CLARITY_FINAL
		#define CLARITY_NULLPTR 0
	#endif
	#define CLARITY_DLLEXPORT __declspec(dllexport)
	#define CLARITY_DLLIMPORT __declspec(dllimport)

	// Hack workaround for Connect issue 1996739
	// Template member functions of template classes can't be parenthesized or reference the
	// fully-qualified type name, or compile fails with C2244
	#define CLARITY_MSVC_ROOT_HACK_START
	#define CLARITY_MSVC_ROOT_HACK_END
#endif

#if defined(__GNUC__ ) || defined(__clang__)
	#if CLARITY_CPP11 != 0
		#define CLARITY_OVERRIDE override
		#define CLARITY_FINAL final
		#define CLARITY_NULLPTR nullptr
	#else
		#define CLARITY_OVERRIDE
		#define CLARITY_FINAL
		#define CLARITY_NULLPTR 0
	#endif
	#define CLARITY_FORCEINLINE inline __attribute__((always_inline))

	//#ifdef __CYGWIN__
	//	#define CLARITY_DLLEXPORT __declspec(dllexport)
	//	#define CLARITY_DLLIMPORT __declspec(dllimport)
	//#else
		#define CLARITY_DLLEXPORT
		#define CLARITY_DLLIMPORT
	//#endif

	#define CLARITY_MSVC_ROOT_HACK_START ( ::
	#define CLARITY_MSVC_ROOT_HACK_END )
#endif

#define CLARITY_PURE = 0

#define CLARITY_NOTIMPLEMENTED throw (::ClarityInternal::InternalNotImplementedException())

#endif
