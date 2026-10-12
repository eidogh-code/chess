@echo off
rem Builds Chess.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:Chess.exe /recurse:src\*.cs
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)
echo Done: Chess.exe
