cd %~dp0
set CONFIG=%1
set Profiler32=..\DotNetProfiler\%CONFIG%\x86\DotNetProfiler.dll
set Profiler64=..\DotNetProfiler\%CONFIG%\x64\DotNetProfiler.dll
if not exist "%Profiler32%" goto :nox86
if not exist "%Profiler64%" goto :nox64
if not exist Profiler32 mkdir Profiler32
if not exist Profiler64 mkdir Profiler64
exit 0

:nox86
echo Please build the x86 version of the DotNetProfiler
exit 1

:nox64
echo Please build the x64 version of the DotNetProfiler
exit 1