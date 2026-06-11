@rem Gradle wrapper script for Windows
@if "%DEBUG%"=="" @echo off
setlocal

if not defined GRADLE_VERSION set "GRADLE_VERSION=8.14.3"
set "BASE_DIR=%~dp0"
set "DIST_DIR=%BASE_DIR%.gradle\wrapper\dists\gradle-%GRADLE_VERSION%-bin"
set "GRADLE_HOME=%DIST_DIR%\gradle-%GRADLE_VERSION%"
set "GRADLE_BIN=%GRADLE_HOME%\bin\gradle.bat"

if exist "%GRADLE_BIN%" goto execute

set "ZIP=%DIST_DIR%\gradle-%GRADLE_VERSION%-bin.zip"
if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"
if not exist "%ZIP%" (
    curl -fsSL "https://services.gradle.org/distributions/gradle-%GRADLE_VERSION%-bin.zip" -o "%ZIP%"
)
tar -xf "%ZIP%" -C "%DIST_DIR%"

:execute
"%GRADLE_BIN%" %*
