; Simple NSIS installer for SharpShot, built from the portable release folder.
; Requires the portable bundle created by Build Release.bat (SharpShot-Release-v1.0).

!include "MUI2.nsh"

!define APP_NAME "SharpShot"
!define APP_PUBLISHER "BmoandShiro"
!define APP_VERSION "1.3.0.1"
!define APP_PORTABLE_DIR "SharpShot-Release-v1.3.0.1"
!define APP_ICON "..\output_color.ico"

; Output installer
OutFile "SharpShot-Setup.exe"

; Default install directory (per-machine Program Files)
InstallDir "$PROGRAMFILES\${APP_NAME}"

; Allow user to change install dir
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"

RequestExecutionLevel admin

!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$INSTDIR"

  ; Copy all files from the portable release into the install directory
  File /r "${APP_PORTABLE_DIR}\*.*"

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
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\${APP_NAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
SectionEnd

