## 설치 / Install

| OS | 받을 파일 / File |
|---|---|
| Windows 10/11 (x64) | `*-windows-x64-setup.exe` (설치 프로그램) 또는 `*-windows-x64.zip` (설치 없이 실행) |
| macOS 11 이상 (Apple Silicon) | `*-macos-arm64.dmg` — 열어서 앱을 Applications로 끌어다 놓으세요 |
| Linux (x64) | `*-linux-x64.tar.gz` — 압축을 풀고 `./install.sh`를 실행하세요 (`--remove`로 제거) |

**Windows**: 설치 프로그램에 아직 코드 서명이 없어서 SmartScreen이 "Windows의 PC 보호" 창을 띄웁니다. **추가 정보 → 실행**을 누르면 설치가 진행됩니다.
The installer isn't code-signed yet, so SmartScreen shows "Windows protected your PC" — choose **More info → Run anyway**.

**macOS**: 앱이 아직 Apple 개발자 서명·공증을 받지 않아서, 처음 열 때 "확인되지 않은 개발자" 경고가 뜹니다. 앱을 **우클릭 → 열기**로 한 번 열거나, 터미널에서 `xattr -dr com.apple.quarantine /Applications/Daylight\ Commander.app` 을 실행하세요. 휴지통으로 보내기는 처음 한 번 Finder 제어를 허락해야 합니다. Intel Mac용 빌드는 아직 없습니다.
The app isn't signed or notarized by Apple yet, so macOS warns about an "unidentified developer" the first time. **Right-click → Open** once, or run `xattr -dr com.apple.quarantine "/Applications/Daylight Commander.app"`. Moving files to the Trash asks once for permission to control Finder. There is no Intel Mac build yet.

**Linux**: X11 또는 XWayland에서 실행됩니다. PDF·동영상·음악 보기(F3)에는 `libwebkit2gtk-4.1`이 필요합니다. 휴지통으로 보내기는 `gio`(GLib) 또는 `trash-put`이 있으면 쓰고, 없으면 영구 삭제로 안내합니다.
Runs on X11 or XWayland. Viewing PDF, video and audio (F3) needs `libwebkit2gtk-4.1`. "Move to Trash" uses `gio` (GLib) or `trash-put` when present, otherwise it offers permanent delete.

받은 파일은 `SHA256SUMS.txt`로 확인할 수 있습니다: `shasum -a 256 -c SHA256SUMS.txt --ignore-missing`

---
