#pragma once
#ifndef __CLARITY_PROJECTS_H__
#define __CLARITY_PROJECTS_H__

#ifdef CLARITY_PROJECT_CORE
	#define CLARITY_COREDLL CLARITY_DLLEXPORT
#else
	#define CLARITY_COREDLL CLARITY_DLLIMPORT
#endif

#endif
