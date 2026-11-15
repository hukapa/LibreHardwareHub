; LibreHardwareHub - instalador Inno Setup 6.
; Compilado por scripts\Publicar-Release.ps1, que publica a aplicacao em bin\Publish-SingleFile
; e passa a versao com /DAppVersion=x.y.z.

#define AppName "LibreHardwareHub"
#define AppExe "LibreHardwareHub.exe"
#define ExitEvent "Global\LibreHardwareHub_Exit_Event"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\Publish-SingleFile"
#endif

[Setup]
AppId={{6B0C5E0E-6C0B-4D7E-9B55-3E1B0A1F7C42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\LibreHardwareMonitor.Windows.Forms\Resources\icon.ico
OutputDir=..\dist
OutputBaseFilename=LibreHardwareHub-v{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; The running copy is closed through its exit event (see [Code]), not by the Restart Manager.
CloseApplications=no

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
portuguese.TaskStartup=Iniciar com o Windows
english.TaskStartup=Start with Windows
portuguese.TaskDriver=Instalar o driver PawnIO (necessario para sensores da motherboard e do CPU)
english.TaskDriver=Install the PawnIO driver (required for motherboard and CPU sensors)
portuguese.StatusDriver=A instalar o driver PawnIO...
english.StatusDriver=Installing the PawnIO driver...
portuguese.StatusClosing=A fechar o LibreHardwareHub em execucao...
english.StatusClosing=Closing the running LibreHardwareHub...

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:TaskStartup}"; Flags: unchecked
Name: "driver"; Description: "{cm:TaskDriver}"

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\PawnIO_setup.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\PawnIO_setup.exe"; Parameters: "-install -silent"; StatusMsg: "{cm:StatusDriver}"; Tasks: driver; Flags: waituntilterminated skipifdoesntexist
Filename: "{app}\{#AppExe}"; Parameters: "--register-startup"; Tasks: startup; Flags: waituntilterminated runhidden
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
; Close the running copy cleanly (fans go back to the BIOS) and remove the logon task before deleting files.
Filename: "{app}\{#AppExe}"; Parameters: "--exit"; RunOnceId: "CloseApp"; Flags: waituntilterminated runhidden
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-startup"; RunOnceId: "RemoveStartup"; Flags: waituntilterminated runhidden

[Code]
const
  EVENT_MODIFY_STATE = $0002;

function OpenEvent(dwDesiredAccess: Cardinal; bInheritHandle: Boolean; lpName: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

// The application owns a named event while it runs. Signalling it asks for a clean shutdown; the event
// disappears when the process is gone, which is how the wait below knows it finished.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Handle: THandle;
  Tries: Integer;
begin
  Result := '';
  Handle := OpenEvent(EVENT_MODIFY_STATE, False, '{#ExitEvent}');
  if Handle = 0 then
    Exit;

  WizardForm.PreparingLabel.Caption := CustomMessage('StatusClosing');
  SetEvent(Handle);
  CloseHandle(Handle);

  for Tries := 1 to 60 do
  begin
    Sleep(250);
    Handle := OpenEvent(EVENT_MODIFY_STATE, False, '{#ExitEvent}');
    if Handle = 0 then
      Exit;
    CloseHandle(Handle);
  end;

  Result := CustomMessage('StatusClosing');
end;
