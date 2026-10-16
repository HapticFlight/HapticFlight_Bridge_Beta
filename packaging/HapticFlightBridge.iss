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
var
  IntegrationPage: TInputDirWizardPage;

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

procedure InitializeWizard;
var
  DcsDefault: String;
  SimHubDefault: String;
begin
  IntegrationPage := CreateInputDirPage(wpSelectDir,
    'Connect DCS World and SimHub',
    'Choose the folders Setup will connect to HapticFlight Bridge.',
    'Select the Saved Games profile DCS uses and the folder containing SimHubWPF.exe. Close both applications before continuing.',
    False, '');
  IntegrationPage.Add('DCS Saved Games profile:');
  IntegrationPage.Add('SimHub installation:');
  DcsDefault := ExpandConstant('{userprofile}\Saved Games\DCS');
  if not DirExists(DcsDefault) then
    DcsDefault := ExpandConstant('{userprofile}\Saved Games\DCS.openbeta');
  SimHubDefault := ExpandConstant('{pf32}\SimHub');
  IntegrationPage.Values[0] := DcsDefault;
  IntegrationPage.Values[1] := SimHubDefault;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = IntegrationPage.ID then
  begin
    if not DirExists(IntegrationPage.Values[0]) then
    begin
      MsgBox('Choose an existing DCS Saved Games profile.', mbError, MB_OK);
      Result := False;
    end
    else if not FileExists(AddBackslash(IntegrationPage.Values[1]) + 'SimHubWPF.exe') then
    begin
      MsgBox('Choose the SimHub folder containing SimHubWPF.exe.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ExitCode: Integer;
  Arguments: String;
begin
  if CurStep = ssPostInstall then
  begin
    Arguments := '--setup-install "' + IntegrationPage.Values[0] + '" "'
      + IntegrationPage.Values[1] + '"';
    if (not ExecAsOriginalUser(ExpandConstant('{app}\HapticFlight Bridge.exe'),
      Arguments, ExpandConstant('{app}'), SW_SHOWNORMAL, ewWaitUntilTerminated, ExitCode))
      or (ExitCode <> 0) then
    begin
      MsgBox('The application was installed, but DCS and SimHub integration did not complete. '
        + 'Open HapticFlight Bridge and use Repair setup after resolving the reported error.', mbError, MB_OK);
      Abort;
    end;
  end;
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
