@echo off
rem Builds Chess.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /out:Chess.exe Chess.cs
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)
echo Done: Chess.exe
