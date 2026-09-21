; GitHub NSIS installer for SharpShot, built from the portable release folder.
; Requires the portable bundle created by Build Release.bat.
; 32-bit makensis must still target 64-bit Program Files.

!include "MUI2.nsh"
!include "x64.nsh"

!define APP_NAME "SharpShot"
!define APP_PUBLISHER "BmoandShiro"
!define APP_VERSION "1.3.26.0"
; Relative to this script (Installer\), so the portable folder in the project root is found.
!define APP_PORTABLE_DIR "..\SharpShot-Release-v1.3.26.0"
!define APP_ICON "..\output_color.ico"

; Output next to the project, not inside Installer\
OutFile "..\SharpShot-Setup.exe"

; Default install directory (64-bit Program Files even when compiled by 32-bit makensis)
InstallDir "$PROGRAMFILES64\${APP_NAME}"

RequestExecutionLevel admin

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetRegView 64
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

; Read a previous install dir after the 64-bit registry view is active.
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"

Section "Install"
  SetRegView 64
  SetOutPath "$INSTDIR"

  ; Copy all files from the portable release into the install directory
  File /r "${APP_PORTABLE_DIR}\*.*"

  CreateShortcut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\SharpShot.exe" "" "$INSTDIR\SharpShot.exe" 0

  ; Write uninstall information
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\SharpShot.exe,0"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
SectionEnd

Section "Uninstall"
  SetRegView 64
  Delete "$SMPROGRAMS\${APP_NAME}.lnk"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\${APP_NAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
SectionEnd

