; Installer of wCalculator (NSIS 3, Modern UI 2).
; Called by build.sh:  makensis -DVERSION=1.0.0 -DSRCDIR=<publish dir> -DICON=<icon.ico> -DOUTFILE=<installer.exe> [-DWELCOMEBMP=<welcome.bmp>] [-DHEADERBMP=<header.bmp>] installer.nsi

Unicode true
!include "MUI2.nsh"

!ifndef VERSION
  !error "VERSION is not defined (use -DVERSION=x.y.z)"
!endif
!ifndef SRCDIR
  !error "SRCDIR is not defined"
!endif
!ifndef ICON
  !error "ICON is not defined"
!endif
!ifndef OUTFILE
  !error "OUTFILE is not defined"
!endif

!define APPNAME "wCalculator"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

Name "${APPNAME} ${VERSION}"
OutFile "${OUTFILE}"
; The folder is fixed (no directory page): the uninstaller can then delete it safely.
InstallDir "$PROGRAMFILES64\${APPNAME}"
InstallDirRegKey HKLM "Software\${APPNAME}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APPNAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} installer"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING

; Artwork: welcome / finish page (164x314) and the banner of the other pages (150x57).
; By default the images next to this script are used (makensis works from the folder of the script, so a bare file name
; is enough); build.sh may pass other ones with -DWELCOMEBMP / -DHEADERBMP.
!ifndef WELCOMEBMP
  !define WELCOMEBMP "welcome.bmp"
!endif
!ifndef HEADERBMP
  !define HEADERBMP "header.bmp"
!endif
!define MUI_WELCOMEFINISHPAGE_BITMAP "${WELCOMEBMP}"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${WELCOMEBMP}"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${HEADERBMP}"
!define MUI_HEADERIMAGE_UNBITMAP "${HEADERBMP}"
!define MUI_HEADERIMAGE_RIGHT

; The title of the welcome and finish pages may need three lines ("Welcome to the setup of wCalculator 1.0.0").
!define MUI_WELCOMEPAGE_TITLE_3LINES
!define MUI_FINISHPAGE_TITLE_3LINES

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APPNAME}.exe"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; The first language is the fallback; the installer starts in the language of the system when it is in the list.
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "Spanish"

Section "Install"
  SetRegView 64
  SetShellVarContext all

  ; An update replaces the previous files.
  RMDir /r "$INSTDIR"
  SetOutPath "$INSTDIR"
  File /r "${SRCDIR}/*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\${APPNAME}.exe"
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${APPNAME}.exe"

  WriteRegStr HKLM "Software\${APPNAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\${APPNAME}.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetRegView 64
  SetShellVarContext all

  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  Delete "$DESKTOP\${APPNAME}.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegKey HKLM "Software\${APPNAME}"
SectionEnd
