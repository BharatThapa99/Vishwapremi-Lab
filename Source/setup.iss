#ifndef Role
  #define Role "Teacher"
#endif
#ifndef OutputRoot
  #define OutputRoot "..\..\outputs\Vishwapremi-Windows"
#endif
#if Role == "Teacher"
  #define AppGuid "{BBA1AE40-AC92-46F6-8AB0-A97A17F80001}"
#else
  #define AppGuid "{BBA1AE40-AC92-46F6-8AB0-A97A17F80002}"
#endif
[Setup]
AppId={{#AppGuid}
AppName=Vishwapremi {#Role}
AppVersion=1.4.0
AppPublisher=Shree Vishwapremi Secondary School
AppComments=Arun 5, Yaku, Bhojpur. Designed by Bharat Thapa.
VersionInfoVersion=1.4.0.0
VersionInfoProductVersion=1.4.0
VersionInfoCompany=Shree Vishwapremi Secondary School
VersionInfoDescription=Vishwapremi {#Role} Setup
DefaultDirName={localappdata}\Programs\Vishwapremi {#Role}
DefaultGroupName=Vishwapremi Lab
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
DisableProgramGroupPage=yes
DisableDirPage=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
UninstallDisplayIcon={app}\Vishwapremi.{#Role}.exe
#if Role == "Student"
CreateUninstallRegKey=no
#endif
OutputDir={#OutputRoot}
OutputBaseFilename=Vishwapremi-{#Role}-Setup
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
InfoBeforeFile=Installer-Notes.txt

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: startup; Description: "Start Vishwapremi {#Role} when this Windows account signs in"; GroupDescription: "Daily use:"

[Files]
Source: "publish\{#Role}\Vishwapremi.{#Role}.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Quick-Start.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "Third-Party-Notices.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Vishwapremi {#Role}"; Filename: "{app}\Vishwapremi.{#Role}.exe"
Name: "{group}\Vishwapremi setup guide"; Filename: "{app}\Quick-Start.html"
Name: "{autodesktop}\Vishwapremi {#Role}"; Filename: "{app}\Vishwapremi.{#Role}.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Vishwapremi{#Role}"; ValueData: """{app}\Vishwapremi.{#Role}.exe"" --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
#if Role == "Student"
Filename: "schtasks.exe"; Parameters: "/create /tn ""VishwapremiStudent"" /tr """"{app}\Vishwapremi.Student.exe"" --startup"" /sc onlogon /rl HIGHEST /f"; Flags: runhidden; Tasks: startup
Filename: "schtasks.exe"; Parameters: "/create /tn ""VishwapremiWatchdog"" /tr """"{app}\Vishwapremi.Student.exe"" --startup"" /sc minute /mo 2 /rl HIGHEST /f"; Flags: runhidden; Tasks: startup
#endif
Filename: "{app}\Vishwapremi.{#Role}.exe"; Description: "Open Vishwapremi {#Role}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
#if Role == "Student"
Filename: "schtasks.exe"; Parameters: "/delete /tn ""VishwapremiStudent"" /f"; Flags: runhidden; RunOnceId: "DelTaskStudent"
Filename: "schtasks.exe"; Parameters: "/delete /tn ""VishwapremiWatchdog"" /f"; Flags: runhidden; RunOnceId: "DelTaskWatchdog"
#endif

[Messages]
WelcomeLabel2=This will install Vishwapremi {#Role} for this Windows account.%n%nShree Vishwapremi Secondary School%nArun 5, Yaku, Bhojpur%n%nDesigned by Bharat Thapa%n%nNo separate Python or .NET installation is required.

[Code]
#if Role == "Student"
function InitializeUninstall(): Boolean;
var
  Form: TSetupForm;
  PromptLabel: TNewStaticText;
  PassEdit: TNewEdit;
  OKButton, CancelButton: TNewButton;
begin
  Result := False;
  Form := CreateCustomForm(ScaleX(360), ScaleY(140), False, True);
  try
    Form.Caption := 'Teacher Authorization Required';

    PromptLabel := TNewStaticText.Create(Form);
    PromptLabel.Parent := Form;
    PromptLabel.Left := ScaleX(16);
    PromptLabel.Top := ScaleY(16);
    PromptLabel.Width := Form.ClientWidth - ScaleX(32);
    PromptLabel.Caption := 'Enter Teacher Password to uninstall Vishwapremi Student:';

    PassEdit := TNewEdit.Create(Form);
    PassEdit.Parent := Form;
    PassEdit.Left := ScaleX(16);
    PassEdit.Top := ScaleY(42);
    PassEdit.Width := Form.ClientWidth - ScaleX(32);
    PassEdit.Height := ScaleY(23);
    PassEdit.PasswordChar := '*';

    OKButton := TNewButton.Create(Form);
    OKButton.Parent := Form;
    OKButton.Caption := 'OK';
    OKButton.ModalResult := mrOk;
    OKButton.Default := True;
    OKButton.Left := Form.ClientWidth - ScaleX(75 + 10 + 75 + 16);
    OKButton.Top := Form.ClientHeight - ScaleY(23 + 16);
    OKButton.Height := ScaleY(23);
    OKButton.Width := ScaleX(75);

    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form;
    CancelButton.Caption := 'Cancel';
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;
    CancelButton.Left := Form.ClientWidth - ScaleX(75 + 16);
    CancelButton.Top := Form.ClientHeight - ScaleY(23 + 16);
    CancelButton.Height := ScaleY(23);
    CancelButton.Width := ScaleX(75);

    Form.ActiveControl := PassEdit;

    if Form.ShowModal = mrOk then
    begin
      if (PassEdit.Text = 'vishwapremi') or (PassEdit.Text = 'VishwapremiLab') or (PassEdit.Text = 'teacher123') or (PassEdit.Text = 'ArunYaku5') then
      begin
        Result := True;
      end
      else
      begin
        MsgBox('Incorrect Teacher Password. Uninstallation has been cancelled.', mbCriticalError, MB_OK);
      end;
    end;
  finally
    Form.Free;
  end;
end;
#endif
