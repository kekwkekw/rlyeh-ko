# Rlyeh Local Korean

사신전기 루루이에 소녀대 X Windows판의 비공식 한국어화 플러그인입니다.
검수한 게임 전용 용어집·UI/스킬 문장집과 **동봉 hdTrans 0.9.5**로 번역합니다.
외부 번역 API, 번역 CDN, XUnity, Rlyeh_TextFix에 의존하지 않습니다.

이 소스는 **v0.2.0 / 2026-11-20 배포 준비본**입니다. 변경 내용은 [v0.2.0 안내](docs/RELEASE-0.2.0.md)에 있습니다.
이미지 한글화와 로컬 이미지 데이터는 [이미지 적용 안내](docs/images.md)를 참고하세요.
검수된 시나리오 행의 우선 치환과 미번역 hdTrans 처리는 [시나리오 적용 안내](docs/scenarios.md)를 참고하세요.
**F2로 이미지·시나리오·Master·UI·hdTrans를 함께 ON/OFF**할 수 있습니다. [전환 안내](docs/toggle.md)
시나리오 이름·세계관 용어의 적용 범위와 확장 절차는 [용어집 안내](docs/glossary.md), 최근 확인 범위와 확장 중단 판단은 [검수 기록](docs/glossary-validation-20261119.md)에 있습니다.
DMM Windows판 Unity 6000.3.5f2에서 설치·복구, 스토리 재생과 대표 메뉴를 검증했습니다.
설치 파일과 대응 소스는 [GitHub Releases](https://github.com/kekwkekw/rlyeh-local-korean/releases)에서 제공합니다.

## 설치 파일

설치 ZIP을 게임 폴더 밖의 새 폴더에 전부 풉니다. [설치·복구 안내](docs/INSTALL.md)를 따라
`VERIFY.cmd`로 검사한 뒤, 게임을 종료하고 `INSTALL.cmd`를 실행해 게임 폴더를 입력합니다.
관리자 권한, 별도 hdTrans 설치, 별도 .NET 런타임 설치는 요구하지 않습니다.
게임은 원래 DMM 실행 경로로 실행합니다.

설정의 `Dictionary`가 비어 있으면 플러그인 안의 사전을 사용합니다.
기존 작업용 설정에 별도 사전 경로가 있다면 값을 비워야 동봉 사전을 사용합니다.
설치 도구는 기존 설정·번역 캐시를 덮어쓰지 않습니다.

게임 자체는 온라인 서비스입니다. 첫 BepInEx 시작에서 Unity 기본 라이브러리 다운로드가 필요할 수 있습니다.
이는 번역 API/CDN과 별개이며, 생성된 interop 및 Unity 라이브러리는 설치 ZIP에 포함하지 않습니다.

## 동작

- F2 전체 전환은 마지막 선택을 저장합니다. 기존 화면은 이동·재로딩 후 반영될 수 있으며 캐시는 유지합니다.
- 스토리는 UTAGE가 확정한 대사 전체를 번역하고 기존 타이핑·스킵·태그·화자 표시를 유지합니다.
- 일반 TMP는 공통 입력 버퍼에서 검수본/준비된 캐시를 렌더링 전에 적용합니다. 옛 TMP setter/OnEnable/탐색 경로와 파견·랭킹 덮어쓰기 코드는 없습니다.
- 처음 보는 문구만 로컬 hdTrans 처리 시간이 필요합니다. 결과는 플러그인 `cache/`에 저장합니다.
- 스킬 설명은 게임이 계산한 수치를 보존합니다. 소환 확인은 실제 재화명·수량을 검수 서식에 넣습니다.
- 폰트는 Noto Serif CJK KR Bold입니다. 이미지 치환은 동봉한 로컬 카탈로그와 PNG를 사용합니다.

## 소스와 빌드

`runtime/`에 플러그인과 코어, `data/canonical/`에 용어집, `data/exports/`에 배포 문장집이 있습니다.
한국어 이미지 카탈로그와 편집 결과는 `data/images/`에 있습니다.
검수된 시나리오 행과 원문 대조 데이터는 `data/scenario/reviewed.json`에 있습니다.
게임 실행 파일·원본 자산 번들·생성 interop, 사용자 로그·캐시·계정 정보, 과거 타사 패치는 저장소에 포함하지 않습니다.

.NET SDK 9 및 본인 게임에서 생성한 BepInEx interop 참조가 필요합니다.

```powershell
.\build.ps1 -GameRoot 'C:\Games\rlyehshoujotaix_cl'
dotnet run --project tests/CoreChecks
```

첫 명령은 플러그인 DLL만 빌드하며 설치하지 않습니다. 두 번째 명령은 기본 Core 검사를 수행합니다.
실제 엔진·네이티브 캡처 검사는 별도의 입력이 필요합니다. 게임 데이터 추출 도구는 자신의 설치본에서만 사용합니다.
패키지 재구성 및 수정된 Il2CppInterop 빌드는 [소스 안내](SOURCE_CODE.md)를 참고하세요.

## 라이선스

Copyright (c) 2026 kekwkekw. 자체 플러그인 코드·설치/빌드 도구·독자 작성 번역 데이터는
**AGPL-3.0-only**로 제공합니다. [LICENSE](LICENSE)를 참고하세요.
게임 원문과 고유 명칭에 대한 권리를 주장하지 않습니다. 게임의 권리는 각 권리자에게 있습니다.
각 외부 구성요소는 [THIRD_PARTY.md](THIRD_PARTY.md)에 명시한 고유 라이선스를 따릅니다.
hdTrans의 제작자는 [소쿠릿(sokcuri)](https://github.com/sokcuri/hdTrans)입니다.
배포 ZIP에 정확한 버전의 플러그인·hdTrans·수정 Il2CppInterop 및 로더 소스를 함께 제공합니다.
