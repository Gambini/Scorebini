
set SCOREBINI_PUB_DIR="%~dp0publish"

if exist %SCOREBINI_PUB_DIR% rmdir /q /s %SCOREBINI_PUB_DIR%

dotnet publish Scorebini/Scorebini.csproj -c Release -p:PublishProfile=FolderProfile -o %SCOREBINI_PUB_DIR%