@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ==================================================
echo P10.12A-1.2 Clean Reinstall

echo This only cleans P10.12A-1 procedural-room scripts.
echo Production_Main / GameScene / room prefabs are NOT deleted.
echo ==================================================
echo.

if not exist "Assets\Scripts\Dungeon\ProceduralRooms" (
  echo [ERROR] Run this .cmd from the Unity project root.
  pause
  exit /b 1
)

set "DST=Assets\Scripts\Dungeon\ProceduralRooms"
set "SRC=_P10_12A1_CleanSource\Assets\Scripts\Dungeon\ProceduralRooms"

echo [1/3] Removing known conflicting P10.12A-1 scripts...
for %%F in (
  "DreamProceduralRoomKernelP1012A1.cs"
  "DreamProceduralRoomKernelP1012A1.cs.meta"
  "DreamProceduralRoomPrototypeP1012A1.cs"
  "DreamProceduralRoomPrototypeP1012A1.cs.meta"
  "DreamProceduralRoomLayout.cs"
  "DreamProceduralRoomLayout.cs.meta"
  "DreamProceduralRoomGenerator.cs"
  "DreamProceduralRoomGenerator.cs.meta"
) do (
  if exist "%DST%\%%~F" del /f /q "%DST%\%%~F"
)

if not exist "%DST%\Editor" mkdir "%DST%\Editor"
for %%F in (
  "DreamProceduralRoomAuditP1012A1.cs"
  "DreamProceduralRoomAuditP1012A1.cs.meta"
  "DreamProceduralRoomPrototypeAuditP1012A1.cs"
  "DreamProceduralRoomPrototypeAuditP1012A1.cs.meta"
) do (
  if exist "%DST%\Editor\%%~F" del /f /q "%DST%\Editor\%%~F"
)

echo [2/3] Installing one authoritative script set...
copy /y "%SRC%\DreamProceduralRoomKernelP1012A1.cs" "%DST%\DreamProceduralRoomKernelP1012A1.cs" >nul
copy /y "%SRC%\DreamProceduralRoomPrototypeP1012A1.cs" "%DST%\DreamProceduralRoomPrototypeP1012A1.cs" >nul
copy /y "%SRC%\Editor\DreamProceduralRoomAuditP1012A1.cs" "%DST%\Editor\DreamProceduralRoomAuditP1012A1.cs" >nul

if errorlevel 1 (
  echo [ERROR] Copy failed. Nothing outside P10.12A-1 was touched.
  pause
  exit /b 1
)

echo [3/3] Done.
echo.
echo Expected authoritative files:
echo   DreamProceduralRoomKernelP1012A1.cs
echo   DreamProceduralRoomPrototypeP1012A1.cs
echo   Editor\DreamProceduralRoomAuditP1012A1.cs
echo.
echo Return to Unity and wait for compilation.
echo Then run:
echo Tools ^> Dream Dungeon ^> Procedural Rooms ^> P10.12A-1 ^> 1B. Repair Existing Prototype Component
pause
exit /b 0
