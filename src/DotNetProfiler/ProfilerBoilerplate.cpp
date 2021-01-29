/*****************************************************************************
 * DotNetProfiler
 * 
 * This file contains the boilerplate part of the profiler implementation.
 *****************************************************************************/

#include <assert.h>
#include "winnt.h"
#include "stdafx.h"
#include "Profiler.h"
// CLR
#include <metahost.h>
#include <winuser.h>
#include <unordered_map>



STDMETHODIMP CProfiler::AppDomainCreationStarted(AppDomainID appDomainID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AppDomainCreationFinished(AppDomainID appDomainID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AppDomainShutdownStarted(AppDomainID appDomainID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AppDomainShutdownFinished(AppDomainID appDomainID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AssemblyLoadStarted(AssemblyID assemblyID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AssemblyLoadFinished(AssemblyID assemblyID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AssemblyUnloadStarted(AssemblyID assemblyID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::AssemblyUnloadFinished(AssemblyID assemblyID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ModuleLoadStarted(ModuleID moduleID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ModuleLoadFinished(ModuleID moduleID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ModuleUnloadStarted(ModuleID moduleID)
{
    return S_OK;
}
	  
STDMETHODIMP CProfiler::ModuleUnloadFinished(ModuleID moduleID, HRESULT hrStatus)
{
	return S_OK;
}

STDMETHODIMP CProfiler::ModuleAttachedToAssembly(ModuleID moduleID, AssemblyID assemblyID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ClassLoadStarted(ClassID classID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ClassLoadFinished(ClassID classID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ClassUnloadStarted(ClassID classID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ClassUnloadFinished(ClassID classID, HRESULT hrStatus)
{
    return S_OK;
}

STDMETHODIMP CProfiler::FunctionUnloadStarted(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITCompilationStarted(FunctionID functionID, BOOL fIsSafeToBlock)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITCompilationFinished(FunctionID functionID, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITCachedFunctionSearchStarted(FunctionID functionID, BOOL *pbUseCachedFunction)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITCachedFunctionSearchFinished(FunctionID functionID, COR_PRF_JIT_CACHE result)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITFunctionPitched(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::JITInlining(FunctionID callerID, FunctionID calleeID, BOOL *pfShouldInline)
{
    return S_OK;
}

STDMETHODIMP CProfiler::UnmanagedToManagedTransition(FunctionID functionID, COR_PRF_TRANSITION_REASON reason)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ManagedToUnmanagedTransition(FunctionID functionID, COR_PRF_TRANSITION_REASON reason)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ThreadCreated(ThreadID threadID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ThreadDestroyed(ThreadID threadID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ThreadAssignedToOSThread(ThreadID managedThreadID, DWORD osThreadID) 
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingClientInvocationStarted()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingClientSendingMessage(GUID *pCookie, BOOL fIsAsync)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingClientReceivingReply(GUID *pCookie, BOOL fIsAsync)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingClientInvocationFinished()
{
	return S_OK;
}

STDMETHODIMP CProfiler::RemotingServerReceivingMessage(GUID *pCookie, BOOL fIsAsync)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingServerInvocationStarted()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingServerInvocationReturned()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RemotingServerSendingReply(GUID *pCookie, BOOL fIsAsync)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON suspendReason)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeSuspendFinished()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeSuspendAborted()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeResumeStarted()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeResumeFinished()
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeThreadSuspended(ThreadID threadID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RuntimeThreadResumed(ThreadID threadID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::MovedReferences(ULONG cmovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::ObjectAllocated(ObjectID objectID, ClassID classID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ObjectsAllocatedByClass(ULONG classCount, ClassID classIDs[], ULONG objects[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::ObjectReferences(ObjectID objectID, ClassID classID, ULONG objectRefs, ObjectID objectRefIDs[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::RootReferences(ULONG rootRefs, ObjectID rootRefIDs[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionThrown(ObjectID thrownObjectID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionUnwindFunctionEnter(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionUnwindFunctionLeave()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionSearchFunctionEnter(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionSearchFunctionLeave()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionSearchFilterEnter(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionSearchFilterLeave()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionSearchCatcherFound(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionCLRCatcherFound()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionCLRCatcherExecute()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionOSHandlerEnter(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionOSHandlerLeave(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionUnwindFinallyEnter(FunctionID functionID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionUnwindFinallyLeave()
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionCatcherEnter(FunctionID functionID,
    											 ObjectID objectID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ExceptionCatcherLeave()
{
    return S_OK;
}

STDMETHODIMP CProfiler::COMClassicVTableCreated(ClassID wrappedClassID, REFGUID implementedIID, void *pVTable, ULONG cSlots)
{
    return S_OK;
}

STDMETHODIMP CProfiler::COMClassicVTableDestroyed(ClassID wrappedClassID, REFGUID implementedIID, void *pVTable)
{
    return S_OK;
}

STDMETHODIMP CProfiler::ThreadNameChanged(ThreadID threadID, ULONG cchName, WCHAR name[])
{
	return S_OK;
}

STDMETHODIMP CProfiler::GarbageCollectionStarted(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason)
{
	return S_OK;
}

STDMETHODIMP CProfiler::SurvivingReferences(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::GarbageCollectionFinished()
{
    return S_OK;
}

STDMETHODIMP CProfiler::FinalizeableObjectQueued(DWORD finalizerFlags, ObjectID objectID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::RootReferences2(ULONG cRootRefs, ObjectID rootRefIDs[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIDs[])
{
    return S_OK;
}

STDMETHODIMP CProfiler::HandleCreated(GCHandleID handleID, ObjectID initialObjectID)
{
    return S_OK;
}

STDMETHODIMP CProfiler::HandleDestroyed(GCHandleID handleID)
{
    return S_OK;
}


STDMETHODIMP CProfiler::InitializeForAttach(IUnknown * pCorProfilerInfoUnk, void * pvClientData, UINT cbClientData)
{	
	return S_OK;
}

STDMETHODIMP CProfiler::ProfilerAttachComplete()
{	
	return S_OK;
}
STDMETHODIMP CProfiler::ProfilerDetachSucceeded()
{	
	return S_OK;
}

