
set SCOREBINI_PUB_DIR="%~dp0publish"

if exist %SCOREBINI_PUB_DIR% rmdir /q /s %SCOREBINI_PUB_DIR%

dotnet publish Scorebini/Scorebini.csproj -c Release -p:PublishProfile=FolderProfile -o %SCOREBINI_PUB_DIR%\net5.0
dotnet publish Scorebini/Scorebini.csproj -c Release -p:PublishProfile=FolderProfile --self-contained true -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o %SCOREBINI_PUB_DIR%\win-x64