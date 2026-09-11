@echo off
chcp 65001 >nul
title MovieManager - Resource Usage Monitor
echo Starting MovieManager Resource Monitor...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Monitor_Resources.ps1"
pause
