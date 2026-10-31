@echo off
setlocal enabledelayedexpansion
set "CSC="
for %%p in (Framework64 Framework) do (
  if exist "C:\Windows\Microsoft.NET\%%p\v4.0.30319\csc.exe" set "CSC=C:\Windows\Microsoft.NET\%%p\v4.0.30319\csc.exe"
)
if "%CSC%"=="" (
  echo ❌ 这台电脑没找到 Windows 自带的 csc 编译器。
  echo    试试：设置 → 应用 → 可选功能 → 安装「.NET Framework 3.5 / 4.x 开发工具」
  echo    装完再双击本文件。或者去仓库 Actions 页面下载现成 exe。
  pause & exit /b 1
)
echo ✅ 找到编译器：%CSC%
"%CSC%" /nologo /unsafe /out:"%~dp0喵喵.exe" "%~dp0喵喵.cs"
if errorlevel 1 ( echo 编译失败 & pause & exit /b 1 )
echo.
echo 🐾 搞定！同目录下已经生成 喵喵.exe，双击它。
echo    （喵喵.png 会跟着 exe 走，不用一起拷贝）
pause
