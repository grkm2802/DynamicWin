@echo off
rem DynamicWin kurulumu: bu dosyaya cift tiklayin. Yonetici izni istemez.
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
pause
