@echo off
chcp 65001 >nul
title VideoHat
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0VideoHat\araclar\baslat.ps1"
if errorlevel 1 pause
