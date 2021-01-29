// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once

#ifndef STRICT
#define STRICT
#endif

#define _ATL_APARTMENT_THREADED
#define _ATL_NO_AUTOMATIC_NAMESPACE
#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS	// some CString constructors will be explicit

#include "resource.h"
#include <assert.h>
#include <atlbase.h>
#include <atlcom.h>
#include <atlctl.h>
#include <WinDef.h>
#include <Windows.h>
#include <winnt.h>
#include <cstdint>
#include <CorHdr.h>
#include <cor.h>
#include <corprof.h>
#include <fstream>
#include <unordered_map>
#include <memory>
#include <metahost.h>
#include <Sddl.h>
#include <set>
#include <string>
#include <sstream>
#include <vector>
#include <limits>
#include <sstream>
#include <algorithm>
#include <functional>
using namespace ATL;