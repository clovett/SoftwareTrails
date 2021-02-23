

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Mon Jan 18 19:14:07 2038
 */
/* Compiler settings for DotNetProfiler.idl:
    Oicf, W1, Zp8, env=Win64 (32b run), target_arch=AMD64 8.01.0622 
    protocol : all , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */



/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */


#ifndef __DotNetProfiler_h__
#define __DotNetProfiler_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __ClrProfiler_FWD_DEFINED__
#define __ClrProfiler_FWD_DEFINED__

#ifdef __cplusplus
typedef class ClrProfiler ClrProfiler;
#else
typedef struct ClrProfiler ClrProfiler;
#endif /* __cplusplus */

#endif 	/* __ClrProfiler_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"
#include "corprof.h"

#ifdef __cplusplus
extern "C"{
#endif 



#ifndef __DotNetProfilerLib_LIBRARY_DEFINED__
#define __DotNetProfilerLib_LIBRARY_DEFINED__

/* library DotNetProfilerLib */
/* [helpstring][version][uuid] */ 


EXTERN_C const IID LIBID_DotNetProfilerLib;

EXTERN_C const CLSID CLSID_ClrProfiler;

#ifdef __cplusplus

class DECLSPEC_UUID("D795A307-4F19-4E49-B714-8641DF72F493")
ClrProfiler;
#endif
#endif /* __DotNetProfilerLib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


