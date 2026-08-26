@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

echo ==========================================================
echo P10.12A-1.3 Global Duplicate Purge + Verified Reinstall
echo This ONLY targets class declarations unique to P10.12A-1.
echo Production_Main / GameScene / prefabs are NOT touched.
echo ==========================================================
echo.

if not exist "Assets" (
  echo [ERROR] This .cmd must be in the Unity project root.
  pause
  exit /b 1
)

set "DST=Assets\Scripts\Dungeon\ProceduralRooms"
set "SRC=_P10_12A1_CleanSource\Assets\Scripts\Dungeon\ProceduralRooms"
set "LOG=P10_12A1_3_PURGE_REPORT.txt"

> "%LOG%" echo P10.12A-1.3 duplicate purge report
>>"%LOG%" echo Project=%CD%
>>"%LOG%" echo.

echo [1/4] Finding ALL P10.12A-1 declaration files under Assets...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$needles=@('public enum DreamProceduralRoomArchetype','public sealed class DreamProceduralDoorLane','public sealed class DreamProceduralRoomLayout','public static class DreamProceduralRoomKernelP1012A1','public sealed class DreamProceduralRoomPrototypeP1012A1','public static class DreamProceduralRoomAuditP1012A1');" ^
  "$files=Get-ChildItem -LiteralPath 'Assets' -Recurse -File -Filter *.cs;" ^
  "$hits=@(); foreach($f in $files){$t=[IO.File]::ReadAllText($f.FullName); foreach($n in $needles){if($t.Contains($n)){$hits += $f.FullName; break}}};" ^
  "$hits=$hits | Sort-Object -Unique;" ^
  "Add-Content -LiteralPath '%LOG%' -Value 'Declaration files found BEFORE purge:'; if($hits.Count -eq 0){Add-Content -LiteralPath '%LOG%' -Value '  (none)'} else {$hits | ForEach-Object {Add-Content -LiteralPath '%LOG%' -Value ('  '+$_)}};" ^
  "foreach($p in $hits){Remove-Item -LiteralPath $p -Force; $m=$p+'.meta'; if(Test-Path -LiteralPath $m){Remove-Item -LiteralPath $m -Force}}"
if errorlevel 1 (
  echo [ERROR] PowerShell purge failed. See %LOG%
  pause
  exit /b 1
)

echo [2/4] Removing known obsolete P10.12A-1 filenames...
if not exist "%DST%" mkdir "%DST%"
if not exist "%DST%\Editor" mkdir "%DST%\Editor"
for %%F in (
  "DreamProceduralRoomLayout.cs" "DreamProceduralRoomLayout.cs.meta"
  "DreamProceduralRoomGenerator.cs" "DreamProceduralRoomGenerator.cs.meta"
  "DreamProceduralRoomPrototypeAuditP1012A1.cs" "DreamProceduralRoomPrototypeAuditP1012A1.cs.meta"
) do if exist "%DST%\%%~F" del /f /q "%DST%\%%~F"
if exist "%DST%\Editor\DreamProceduralRoomPrototypeAuditP1012A1.cs" del /f /q "%DST%\Editor\DreamProceduralRoomPrototypeAuditP1012A1.cs"
if exist "%DST%\Editor\DreamProceduralRoomPrototypeAuditP1012A1.cs.meta" del /f /q "%DST%\Editor\DreamProceduralRoomPrototypeAuditP1012A1.cs.meta"

echo [3/4] Installing the one authoritative script set...
copy /y "%SRC%\DreamProceduralRoomKernelP1012A1.cs" "%DST%\DreamProceduralRoomKernelP1012A1.cs" >nul
copy /y "%SRC%\DreamProceduralRoomPrototypeP1012A1.cs" "%DST%\DreamProceduralRoomPrototypeP1012A1.cs" >nul
copy /y "%SRC%\Editor\DreamProceduralRoomAuditP1012A1.cs" "%DST%\Editor\DreamProceduralRoomAuditP1012A1.cs" >nul
if errorlevel 1 (
  echo [ERROR] Copy failed.
  pause
  exit /b 1
)

echo [4/4] Verifying declaration counts under Assets...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$defs=@{'Archetype'='public enum DreamProceduralRoomArchetype';'DoorLane'='public sealed class DreamProceduralDoorLane';'Layout'='public sealed class DreamProceduralRoomLayout';'Kernel'='public static class DreamProceduralRoomKernelP1012A1';'Prototype'='public sealed class DreamProceduralRoomPrototypeP1012A1';'Audit'='public static class DreamProceduralRoomAuditP1012A1'};" ^
  "$files=Get-ChildItem -LiteralPath 'Assets' -Recurse -File -Filter *.cs; $bad=$false;" ^
  "Add-Content -LiteralPath '%LOG%' -Value ''; Add-Content -LiteralPath '%LOG%' -Value 'Declaration counts AFTER reinstall:';" ^
  "foreach($k in $defs.Keys){$paths=@(); foreach($f in $files){$t=[IO.File]::ReadAllText($f.FullName); if($t.Contains($defs[$k])){$paths += $f.FullName}}; $c=$paths.Count; Add-Content -LiteralPath '%LOG%' -Value ('  '+$k+'='+$c); $paths | ForEach-Object {Add-Content -LiteralPath '%LOG%' -Value ('    '+$_)}; if($c -ne 1){$bad=$true}}; if($bad){exit 7}else{exit 0}"
if errorlevel 1 (
  echo [ERROR] Verification FAILED. Do NOT open Unity yet.
  echo Send me: %LOG%
  pause
  exit /b 1
)

echo.
echo PASS: every P10.12A-1 declaration exists exactly once under Assets.
echo Report: %LOG%
echo.
echo Expected files:
echo   %DST%\DreamProceduralRoomKernelP1012A1.cs
echo   %DST%\DreamProceduralRoomPrototypeP1012A1.cs
echo   %DST%\Editor\DreamProceduralRoomAuditP1012A1.cs
echo.
echo Now reopen Unity. Safe Mode should compile cleanly.
echo Then run P10.12A-1 ^> 1B. Repair Existing Prototype Component.
pause
exit /b 0
