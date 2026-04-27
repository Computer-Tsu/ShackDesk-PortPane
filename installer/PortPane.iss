; PortPane by ShackDesk — Inno Setup installer script
; Inno Setup 6.x required: https://jrsoftware.org/isinfo.php
;
; Build from CI: ISCC.exe installer\PortPane.iss
; Output: installer\Output\PortPane-Setup-x.x.x.exe
;
; See BrandingInfo.cs for all values sourced here.

#define AppName        "PortPane"
#define AppVersion     "0.5.8"
#define AppPublisher   "My Computer Guru LLC"
#define AppURL         "https://shackdesk.com"
#define AppExeName     "PortPane.exe"
#define AppDescription "Takes the pain out of ports"
#define CopyrightYear  "2024-2026"

[Setup]
AppId={{F8A2C4E6-3B1D-4F9A-8E7C-5D2B0A6C1E4F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL=https://github.com/Computer-Tsu/shackdesk-portpane/discussions
AppUpdatesURL={#AppURL}
AppCopyright=Copyright (C) {#CopyrightYear} Mark McDow (N4TEK). All rights reserved.
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE-MIT.md
InfoBeforeFile=..\docs\INSTALLER_NOTE.txt
OutputDir=Output
OutputBaseFilename=PortPane-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 minimum
MinVersion=10.0.17763

; Code signing placeholder
; SignTool=standard sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon";  Description: "Create Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main executable (single self-contained exe from dotnet publish)
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; SHA-256 hash file for integrity verification
Source: "..\publish\{#AppExeName}.sha256"; DestDir: "{app}"; Flags: ignoreversion

; USB device database (bundled — also shipped as separate artifact for updates)
Source: "..\data\usb_devices.json"; DestDir: "{app}\Data"; Flags: ignoreversion

; Legal and service files
Source: "..\LICENSE-MIT.md";                    DestDir: "{app}"; Flags: ignoreversion
Source: "..\OFFICIAL_BUILDS_AND_SERVICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";            Filename: "{app}\{#AppExeName}"; Comment: "{#AppDescription}"
Name: "{group}\Uninstall {#AppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Comment: "{#AppDescription}"; Tasks: desktopicon
Name: "{userstartmenu}\{#AppName}";    Filename: "{app}\{#AppExeName}"; Comment: "{#AppDescription}"; Tasks: startmenuicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove settings directory on uninstall only if user confirms
; (handled by the app itself via a prompt — installer does not force-delete)
Type: dirifempty; Name: "{localappdata}\ShackDesk\PortPane"

[Code]
// Verify SHA-256 of the installed exe after copy
procedure CurStepChanged(CurStep: TSetupStep);
var
  ExePath, HashPath, StoredHash: string;
begin
  if CurStep = ssPostInstall then
  begin
    ExePath  := ExpandConstant('{app}\{#AppExeName}');
    HashPath := ExpandConstant('{app}\{#AppExeName}.sha256');
    // Placeholder only. Full SHA-256 verification would require a PowerShell call
    // or custom DLL. The build pipeline already verifies and packages the hash.
    // Temporarily disabled while stabilizing CI installer creation.
    // if FileExists(HashPath) then
    // begin
    //   LoadStringFromFile(HashPath, StoredHash);
    // end;
  end;
end;
