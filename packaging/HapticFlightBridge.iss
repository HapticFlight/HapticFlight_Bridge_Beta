#ifndef StageRoot
  #error StageRoot must point to the verified Setup staging directory.
#endif
#ifndef ProductVersion
  #error ProductVersion is required.
#endif
#ifndef OutputRoot
  #error OutputRoot is required.
#endif
#ifndef SetupFileBase
  #error SetupFileBase is required.
#endif

[Setup]
AppId={{E24DC0B3-70C1-4CDE-B89F-2C6571F1ECF4}
AppName=HapticFlight Bridge
AppVersion={#ProductVersion}
AppVerName=HapticFlight Bridge {#ProductVersion}
DefaultDirName={localappdata}\Programs\HapticFlight Bridge
DefaultGroupName=HapticFlight Bridge
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputRoot}
OutputBaseFilename={#SetupFileBase}
SetupIconFile=..\tools\DcsExtraTelemetry.Launcher\Assets\HapticFlightOrangeIcon.ico
UninstallDisplayIcon={app}\HapticFlight Bridge.exe
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
MinVersion=10.0
#ifdef ReleaseSigned
SignTool=bridge
SignedUninstaller=yes
#endif

[Files]
Source: "{#StageRoot}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\HapticFlight Bridge"; Filename: "{app}\HapticFlight Bridge.exe"
Name: "{autodesktop}\HapticFlight Bridge"; Filename: "{app}\HapticFlight Bridge.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\HapticFlight Bridge.exe"; Description: "Launch HapticFlight Bridge"; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
function InitializeSetup: Boolean;
var
  FrameworkRelease: Cardinal;
begin
  if IsWin64 then
    Result := RegQueryDWordValue(HKLM64,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', FrameworkRelease)
      and (FrameworkRelease >= 528040)
  else
    Result := RegQueryDWordValue(HKLM32,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', FrameworkRelease)
      and (FrameworkRelease >= 528040);
  if not Result then
    MsgBox('HapticFlight Bridge requires the Microsoft .NET Framework 4.8 Desktop Runtime or newer. '
      + 'Install it from Microsoft, then run Setup again.', mbError, MB_OK);
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\HapticFlight Bridge.exe')) then
  begin
    if (not Exec(ExpandConstant('{app}\HapticFlight Bridge.exe'), '--setup-uninstall',
      ExpandConstant('{app}'), SW_SHOWNORMAL, ewWaitUntilTerminated, ExitCode))
      or (ExitCode <> 0) then
    begin
      MsgBox('The DCS or SimHub integration could not be removed. Application removal was stopped '
        + 'so the installation receipt and recovery files remain available.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;
