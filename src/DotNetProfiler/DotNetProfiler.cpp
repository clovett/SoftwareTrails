// DotNetProfiler.cpp : Implementation of DLL Exports.


#include "stdafx.h"
#include "resource.h"
#include "DotNetProfiler.h"

class CDotNetProfilerModule : public CAtlDllModuleT< CDotNetProfilerModule >
{
public :
	DECLARE_LIBID(LIBID_DotNetProfilerLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_DOTNETPROFILER, "{76BC1F40-3772-45C2-895E-27F6371CAEC9}")
};

CDotNetProfilerModule _AtlModule;


#ifdef _MANAGED
#pragma managed(push, off)
#endif

// DLL Entry Point
extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
	//MessageBox(NULL, L"The profiler is loaded, so you can debug it now", L"Profiler Debug Prompt", MB_ICONINFORMATION);
	
	return _AtlModule.DllMain(dwReason, lpReserved); 
}

#ifdef _MANAGED
#pragma managed(pop)
#endif




// Used to determine whether the DLL can be unloaded by OLE
STDAPI DllCanUnloadNow(void)
{
    return _AtlModule.DllCanUnloadNow();
}


// Returns a class factory to create an object of the requested type
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    return _AtlModule.DllGetClassObject(rclsid, riid, ppv);
}


// DllRegisterServer - Adds entries to the system registry
STDAPI DllRegisterServer(void)
{
	//MessageBox(NULL, L"The profiler is loaded so you can debug it now.", L"Profiler Debug Prompt", MB_ICONINFORMATION);

    // registers object, typelib and all interfaces in typelib
    HRESULT hr = _AtlModule.DllRegisterServer();
	return hr;
}


// DllUnregisterServer - Removes entries from the system registry
STDAPI DllUnregisterServer(void)
{
	HRESULT hr = _AtlModule.DllUnregisterServer();
	return hr;
}

