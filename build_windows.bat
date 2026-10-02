@echo off
setlocal
where dotnet >nul 2>nul || (echo .NET 8 SDK is required. Download: https://dotnet.microsoft.com/download/dotnet/8.0 & exit /b 1)
dotnet restore MYVOCAL-Studio.sln || exit /b 1
dotnet build MYVOCAL-Studio.sln -c Release --no-restore || exit /b 1
dotnet run --project tests\MyVocalStudio.Tests -c Release --no-build || exit /b 1
dotnet publish src\MyVocalStudio\MyVocalStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist || exit /b 1
if not exist dist\MYVOCAL-Studio.exe (echo EXE build failed. & exit /b 1)
echo Build complete: dist\MYVOCAL-Studio.exe
endlocal
