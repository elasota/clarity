#pragma once
#ifndef __CLARITY_EQUALITY_COMPARE_FUNC_H__
#define __CLARITY_EQUALITY_COMPARE_FUNC_H__

#include "ClarityInternalSupport.h"

namespace ClarityInternal
{
	template<class TA, class TB>
	struct TAreEqualFunc
		: public ::ClarityInternal::TypeDef<bool (*)(const TA &a, const TB &b)>
	{
	};
}

#endif
