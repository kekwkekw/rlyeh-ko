# 설치와 복구

이 문서의 경로 `C:\Games\rlyehshoujotaix_cl`은 예시입니다. 실제 `rlyehshoujotaix_cl.exe`가 있는 폴더를 사용하세요.

## 설치

1. **압축 해제 전 ZIP 차단 해제(권장)**: 브라우저에서 받은 설치 ZIP 파일을 마우스 오른쪽 클릭 → **속성** → **일반** 탭 하단의 **보안: `차단 해제` 체크 → 적용/확인**을 누릅니다.
   - 반드시 **압축을 풀기 전** ZIP 파일에 적용해야 내부 스크립트(`INSTALL.cmd`, `Install.ps1`)와 DLL 전체의 인터넷 차단 표시가 함께 제거되어 SmartScreen 경고 및 DLL 로드 오류를 예방할 수 있습니다.
   - `차단 해제` 항목이 표시되지 않으면 이미 차단 표시가 없는 상태이므로 그대로 진행합니다.
   - 이미 압축을 푼 상태라면 압축 해제 폴더를 지우고 ZIP에 차단 해제를 적용한 뒤 다시 푸는 것이 가장 확실합니다.
2. 게임을 종료하고 ZIP 전체를 게임 폴더 밖에 풉니다. `payload`만 수동 복사하면 자동 백업 기록이 생기지 않습니다.
3. 기존 다른 번역 패치가 있다면 게임 밖에 백업하고 먼저 제거합니다. 설치 도구는 다른 플러그인/patcher DLL을 발견하면 중단하며 자동 삭제하지 않습니다.
4. `VERIFY.cmd`로 파일 무결성을 검사합니다.
5. `INSTALL.cmd`를 실행합니다. (`Windows의 PC 보호` 창이 뜨면 `추가 정보` → `실행`을 누릅니다.)
   - 일반적인 DMM/게임 경로에 `rlyehshoujotaix_cl.exe`가 감지되면 기본 경로로 제안합니다. 그대로 `Enter`를 누르거나, `B`를 입력해 Windows 폴더 선택 창에서 고르거나, 폴더 경로를 직접 붙여넣을 수 있습니다.
   - 설치 도구는 변경될 파일의 이전 바이트를 `게임폴더/RlyehPatchBackups/<시각-ID>/before/`에 저장하고 `manifest.json`을 기록합니다.
6. 평소 사용하던 DMM 런처로 실행합니다. 첫 실행은 BepInEx interop 생성 때문에 오래 걸릴 수 있습니다. 번역에 필요한 hdTrans DLL과 사전은 모두 동봉합니다.

PowerShell에서 직접 실행할 수도 있습니다. Windows PowerShell 5.1과 PowerShell 7을 지원 대상으로 합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -Mode VerifyPackage
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -Mode Plan -GameRoot 'C:\Games\rlyehshoujotaix_cl'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -Mode Install -GameRoot 'C:\Games\rlyehshoujotaix_cl'
```

`Plan`은 대상 파일을 비교하고 변경 개수만 출력합니다. 대상 게임 폴더에 쓰지 않습니다.
실행 정책 옵션은 해당 프로세스에만 적용되며 시스템 정책을 변경하지 않습니다.
새 패키지를 풀 때 이전 패키지 폴더 위에 덮어 풀지 마세요.

## v0.1.0 또는 RC3에서 업데이트

게임을 종료하고 v0.2.0 ZIP을 새 폴더에 풀어 같은 게임 경로에 설치합니다.
플러그인·용어집을 갱신하고 이미지와 검수 시나리오 데이터를 추가하며, 기존 설정·캐시는 보존합니다.
업데이트가 기록한 새 `manifest.json`으로 복구하면 업데이트 이전 버전의 관리 파일로 돌아갑니다.
이번 버전은 이전과 관리 파일이 같지 않으므로 새 복구 기록을 보관하세요.

동일한 v0.2.0을 다시 설치하면 파일이 모두 일치할 때 변경 0건을 표시합니다.
이 경우 새 복구 기록을 만들지 않으며, 원래 설치의 `manifest.json`을 사용합니다.

## 기존 RlyehTextFix에서 이전

게임을 종료하고 기존 BepInEx·XUnity·RlyehTextFix·설정·캐시·dotnet 등 예전 패치 파일을
게임 폴더 밖에 백업한 뒤 제거합니다. 게임 실행 파일과 원본 자산은 보존하세요.
이 패키지를 새로 설치하면 레거시 DLL이나 번역 CDN 없이 동봉 hdTrans로 동작합니다.
기존 XUnity 캐시를 새 플러그인의 `cache/`에 복사하지 마세요.
이전 설치의 파일 구성이 불확실하면 DMM의 게임 파일 복구 후 새 패치를 설치합니다.

## 설정과 동봉 사전

최초 실행 후 `BepInEx/config/local.rlyeh.korean.cfg`가 생성됩니다.

```ini
[General]
Enabled = true

[Images]
Enabled = true

[Scenarios]
Enabled = true

[Local]
Dictionary =
TranslateUi = true

[Diagnostics]
TranslationDelayMs = 0
TraceDisplay = false
TraceCatalog = false
```

기존 설정이 있으면 보존합니다. `Dictionary`가 비어 있어야
`BepInEx/plugins/Rlyeh.Localizer/native/hdtrans-data/dicts.hdsnap`을 사용합니다.
값을 지정하면 그 로컬 파일을 사용하며, 상대 경로는 플러그인 폴더 기준입니다.
원문 유지/오류가 발생해도 온라인 번역으로 전환하지 않습니다.
용어집과 사전·엔진이 달라지면 별도 캐시를 사용합니다. 업데이트는 게임을 종료하고 진행합니다.
F2로 한국어화를 전체 전환하며 마지막 선택을 저장합니다. 기존 화면은 이동·재로딩 후 반영될 수 있습니다.
업데이트 이전에 F2로 꺼둔 상태라면 업데이트 후에도 OFF이며 F2로 다시 켤 수 있습니다.

## 복구

게임을 종료한 뒤 아래 두 방법 중 하나로 복구합니다. 백업은 원래 게임 폴더 안의 위치를 유지해야 합니다.

1. **`RESTORE.cmd` 실행(간편 복구)**: `RESTORE.cmd`를 더블클릭하면 게임 폴더와 `RlyehPatchBackups` 안의 최신 백업 영수증 목록을 표시합니다. 번호 선택(기본 `[1]`) 또는 `B`(파일 선택 창)로 `manifest.json`을 고른 뒤 `Y`를 입력해 복구합니다.
   - 특정 백업의 `manifest.json` 파일을 `RESTORE.cmd` 아이콘 위로 끌어다 놓아 바로 복구할 수도 있습니다.
2. **PowerShell 직접 실행**: 설치 출력에 나온 `manifest.json` 경로를 직접 지정합니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -Mode Restore -Receipt 'C:\Games\rlyehshoujotaix_cl\RlyehPatchBackups\<시각-ID>\manifest.json'
```

해당 설치에서 추가한 파일은 제거하고, 덮어쓴 파일은 이전 바이트로 돌립니다.
사용자가 설치 후 관리 파일을 수정했다면 복구 전에 중단합니다. 변경본을 먼저 별도 보존하세요.
실행 중 생성된 캐시·설정·interop·로그는 유지하며 게임 실행 파일과 자산을 수정하지 않습니다.
여러 버전을 설치했다면 최신 백업부터 역순으로 복구합니다. 설치가 중간에 실패한 경우에도 이미 기록된 receipt로 복구할 수 있습니다.

## 문제 확인

`BepInEx/LogOutput.log`에서 `Chainloader startup complete`, `Using bundled hdTrans dictionary`,
`hdTrans ready 0.9.5`, `Dynamic font ready`, `TMP Korean font ready`를 확인합니다.
현재 Unity 6000.3.5f2에서 기존 `Class::Init ... substitute` 경고는 알려진 대체 경로입니다.
새 게임 빌드의 Unity/IL2CPP 구조가 달라지면 재검증이 필요합니다.
문제 신고에는 오류 부분만 사용하고 계정·인증 정보가 섞인 전체 로그를 그대로 공개하지 마세요.
