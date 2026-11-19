# Rlyeh Local Korean v0.2.0

2026-11-20 배포 준비본. Windows판 Unity 6000.3.5f2를 대상으로 한다.
설치 파일은 `Rlyeh-Local-Korean-0.2.0-hdTrans-0.9.5.zip`, 대응 소스는
`rlyeh-localizer-0.2.0-source.zip`이며 `SHA256SUMS.txt`로 두 ZIP을 검사한다.
게시된 파일은 [공식 저장소 Releases](https://github.com/kekwkekw/rlyeh-local-korean/releases)에서 확인한다.

## v0.1.0 이후 변경

- 타이틀 로고, 로딩 캐릭터 소개 4장, 승리·패배 본문/발광, 아포칼립스 카드/진입 제목을 포함한
  이미지 19장을 로컬 파일로 한국어화한다. 사용자가 완성한 최종 PNG를 그대로 포함한다.
- 검수된 UTAGE 시나리오 행을 명령 생성 전에 치환한다. 기존 일본어 캐시보다 검수본이 우선하며,
  번역본이 없는 대사만 기존 캐시 또는 동봉 hdTrans로 처리한다. 초기 검수본은 7행이다.
- F2로 이미지·시나리오·Master·UI·hdTrans를 함께 ON/OFF한다. 원문과 원래 이미지 참조를 보존하며
  기존 화면은 이동·재로딩 후 반영될 수 있다. 마지막 선택을 저장한다.
- 시나리오 이름·세계관 용어를 검수하고 용어집을 504개로 확장했다.
  hdTrans names 151개, userdict 90개, local exact 413개를 생성한다.
  같은 원문인 AutoSkill 12·24의 밀어내기 표현도 통일했다.
- 아포칼립스 보상 안내의 클래스 아이콘 공간을 복원하고 실제 한글 글자 폭에 맞춰 배치한다.
  F2 OFF에서는 일본어 원래 위치로 복원한다.
- 설치·복구 도구가 이미지 20개 파일(카탈로그+PNG 19장)과 시나리오 파일까지 관리한다.

번역 엔진은 **동봉 hdTrans 0.9.5**를 유지한다. 번역 API나 번역 CDN은 사용하지 않는다.
Master/UI 런타임 문장집은 4,032개다. 시나리오 전체 번역본을 제공하는 버전은 아니며,
새 대사는 로컬 hdTrans의 번역 품질에 따른다. 처음 보는 문구에는 로컬 처리 시간이 필요하다.
게임 자체의 온라인 접속과 최초 BepInEx Unity 라이브러리 준비는 별개다.

## 설치·업데이트·복구

게임을 종료하고 ZIP 전체를 게임 밖의 새 폴더에 푼다. `VERIFY.cmd` 확인 후 `INSTALL.cmd`에서
게임 경로를 지정한다. v0.1.0/RC3 사용자는 같은 경로에 설치하면 설정·캐시를 보존하면서 갱신한다.
설치가 출력한 새 `RlyehPatchBackups/.../manifest.json`을 보관한다.
복구는 동봉 `Install.ps1 -Mode Restore -Receipt <manifest.json>`을 사용한다.

기존 RlyehTextFix/XUnity 사용자는 레거시 패치를 게임 밖에 백업·제거한 뒤 새로 설치한다.
자세한 순서와 설정은 [설치·복구 안내](INSTALL.md)에 있다.
라이선스와 대응 소스는 [SOURCE_CODE.md](../SOURCE_CODE.md), [THIRD_PARTY.md](../THIRD_PARTY.md)를 참고한다.

## 기능별 검증 근거

[이미지](image-validation-20261120.md), [시나리오](scenario-validation-20261118.md),
[F2](toggle-validation-20261118.md), [용어집](glossary-validation-20261119.md),
[클래스 아이콘](apocalypse-class-icon-validation-20261120.md) 기록은 각 변경 단계의 확인 범위를 설명한다.
전체 게임의 모든 메뉴·시나리오를 검수했다는 의미는 아니다.
