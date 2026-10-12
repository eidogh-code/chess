@echo off
rem Builds and runs the chess rules tests.
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /optimize+ /codepage:65001 /out:bin\RulesTests.exe /recurse:src\Rules\*.cs tests\RulesTests.cs
if errorlevel 1 exit /b 1
bin\RulesTests.exe
