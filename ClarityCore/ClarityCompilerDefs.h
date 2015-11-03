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
#endif

#define CLARITY_PURE = 0

#endif
