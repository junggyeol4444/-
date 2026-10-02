@echo off
setlocal
where py >nul 2>nul || (echo Python 3 is required. & exit /b 1)
py -m pip install --upgrade pip pyinstaller
if errorlevel 1 exit /b 1
pyinstaller --noconfirm --clean MYVOCAL.spec
if errorlevel 1 exit /b 1
echo.
echo Build complete: dist\MYVOCAL-Studio.exe
endlocal
