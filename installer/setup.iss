#define AppName "ChecksumVerify"
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString('..\publish\win-x64\ChecksumVerify.exe')
#endif
#define AppPublisher "Heiko Luxenhofer"
#define AppURL "https://github.com/hluxenhofer/checksum-verify"
#define AppExeName "ChecksumVerify.exe"
#define AppId "A8F3C2D1-4B7E-4F9A-8C3D-2E5F6A7B8C9D"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ChecksumVerify"; \
    ValueType: string; ValueName: ""; ValueData: "Verify Checksum"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ChecksumVerify"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ChecksumVerify\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, 'SOFTWARE\Classes\*\shell\ChecksumVerify');
  end;
end;
