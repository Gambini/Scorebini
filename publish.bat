
set SCOREBINI_PUB_DIR="%~dp0publish"
set FW_NAME=net6.0
set WIN64_NAME=win-x64
set FRAMEWORK_PUB_DIR=%SCOREBINI_PUB_DIR%\%FW_NAME%
set WIN64_PUB_DIR=%SCOREBINI_PUB_DIR%\%WIN64_NAME%
set VERSION_STRING=v1.0.0

if not [%1]==[] set VERSION_STRING=%1

if exist %SCOREBINI_PUB_DIR% rmdir /q /s %SCOREBINI_PUB_DIR%

dotnet publish Scorebini/Scorebini.csproj -c Release -p:PublishProfile=FolderProfile -o %FRAMEWORK_PUB_DIR%
dotnet publish Scorebini/Scorebini.csproj -c Release -p:PublishProfile=FolderProfile --self-contained true -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o %WIN64_PUB_DIR%

echo "Compressing output"

powershell Compress-Archive -Path %FRAMEWORK_PUB_DIR%\* -DestinationPath %SCOREBINI_PUB_DIR%\Scorebini-%VERSION_STRING%.zip
powershell Compress-Archive -Path %WIN64_PUB_DIR%\* -DestinationPath %SCOREBINI_PUB_DIR%\Scorebini-%VERSION_STRING%-%WIN64_NAME%.zip

echo "Complete"