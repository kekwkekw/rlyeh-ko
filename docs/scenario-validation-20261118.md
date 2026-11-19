# 시나리오 행 치환 구현·검증 — 2026-11-18

이미지 단계에 이어 로컬 개발 브랜치에서 구현했다. 원본 게임 book/번들은 수정하지 않는다.
시나리오 전체 번역 작업은 수행하지 않았으며, 직접 검수한 초기 예시 7행을 포함한다.
공개 push, main 병합, 릴리스 및 설치 ZIP 생성은 이 단계에 포함하지 않는다.

## 구현

- monmusu의 [Hooks.cs](https://github.com/kekwkekw/monmusu-td-ko/blob/main/plugin/MonmusuMod/Hooks.cs)와
  [TranslationPackage.cs](https://github.com/kekwkekw/monmusu-td-ko/blob/main/plugin/MonmusuMod.Core/TranslationPackage.cs)를 참고했다.
- `CreateCommandList` 전에 Text 셀을 치환한다. 실제 `AdvCommandText.DoCommand` 직전에도 정책을 확인해
  이미 생성된 명령 목록에서 원문 복원·검수본 적용이 가능하다.
- 정확한 grid 이름·행 위치·RowIndex·헤더·원문 셀 전체를 대조한다. 다른 셀은 쓰지 않는다.
- 검수본과 원문은 `scenario/reviewed.json`에 함께 보존한다. 번역 이미지와 동일하게 로컬 파일만 사용한다.
- 표시 어댑터는 현재 페이지의 native 행 순서를 받아 검수 구간을 캐시와 hdTrans에서 제외한다.
  검수 행이 없는 페이지의 기존 전체 요청, 캐시 키와 사전 revision은 유지했다.
- 혼합 페이지는 미검수 행만 번역하고 원래 순서로 합친다. RevisionSlot의 늦은 응답 거부를 유지한다.
- `SetEnabled(bool)`은 후속 F2에 연결할 지점이다. 이번 단계에서 F2 전체 전환을 구현하지 않았다.

원문 복원 실기에서 문자열 파서 진입점만으로는 재사용 명령의 셀이 복원되지 않았다.
그 후킹은 제거하고 실제 대사 명령 실행 진입점으로 교체했다. 두 경로를 겹쳐서 시험하지 않았다.

## 실기 결과

| 확인 항목 | 결과 |
|---|---|
| BepInEx / IL2CPP / hdTrans 0.9.5 시작 | 정상. 이미지 19개와 시나리오 카탈로그 로드 |
| 캐시보다 검수본 우선 | 초기 6행 모두 원문에 대한 기존 영속 캐시가 존재하는 상태에서 확인 |
| 프롤로그 첫 대사 | native 본문 `하아…… 하아…….` / reviewed=1, engineParts=0 |
| 검수된 2줄 음성 대사 | native 본문 자체가 한국어. 원문 화자 ID와 Voice 경로 유지 |
| 미검수 긴 대사 | reviewed=0, engineParts=1, 기존 캐시 ready=True로 처리. 끝까지 표시 |
| 미검수 캐릭터 대사 | 캐시 없는 대사가 ready=False로 로컬 hdTrans 처리되는 것 확인 |
| 명령 재사용 중 원문 복원 | 진단 F9 비활성 후 다음 대사의 native 본문이 일본어 원문으로 복원됨 |
| 검수 정책 재활성화 | 이후 검수 대사는 다시 reviewed=1, engineParts=0 |
| interval 검수 행 | native 본문에 `<interval=0.8>` 유지, 한국어 두 줄 끝까지 표시 |
| 검수 행 백로그 | native 백로그에서도 한글·줄바꿈 정상 |
| 실제 Next 혼합 페이지 | 최종 정상 빌드에서 reviewed=1, engineParts=1. 둘째 행만 ready=False 번역, 첫 검수 행 유지 |
| 스킵·즉시 표시·페이지 전환 | 기존 버튼으로 진행·복귀 확인 |

원문 복원 검사는 **행 치환 정책만** 끈 검사다. 기존 hdTrans는 계속 켜져 있으므로 표시 화면은
기존 캐시의 한국어가 될 수 있다. 로그의 native 본문과 SCENARIO restored 기록으로 원문 복원을 확인했다.

혼합 페이지는 `10001A1_Nml`의 35행(PageCtrl=Next, 검수)과 53행(미검수)이다.
원래 일본어 두 행을 합친 이전 캐시는 그대로 남아 있다. 최종 실행 후 캐시를 대조해
미검수 53행의 단독 캐시가 생겼고, 검수본이 섞인 전체 페이지는 캐시되지 않았음을 확인했다.
첫 행의 한국어와 interval은 그대로 유지됐으며, 둘째 행 번역을 포함해 끝까지 표시됐다.

## 자동 검사와 설치

- CoreChecks 69개 통과. 행·헤더·비텍스트 셀 대조, 제어 토큰, 검수본 캐시 우회,
  혼합 페이지의 비동기 순서·실패·이전 페이지 결과 거부를 포함한다.
- Python 기존 테스트 13개 통과.
- 실제 동봉 hdTrans 서비스 검사 통과. 50ms 진단 지연에서 요청 완료, 태그 보존,
  새 대사를 덮어쓰지 않는 늦은 응답 거부를 확인했다.
- 초안 도구로 실제 book의 79개 원문 행 추출 확인. 검수 7행 컴파일 왕복 결과의 JSON 바이트 일치.
- 정상 빌드 오류 0, NuGet 취약점 메타데이터 접근 경고 NU1900 2건.
- 카탈로그는 소스 ZIP 입력과 설치 payload에 모두 포함하도록 패키징을 수정했다.
- 최종 DLL에는 ScenarioProbe/F9와 ImageProbe/F8 코드가 들어가지 않는다.
- 설치된 DLL·Core·시나리오 JSON과 기존 이미지 20개 파일이 개발 결과와 바이트 단위로 일치한다.
- 공개 소스 감사 119개 파일 및 공백 오류 검사 통과.
- 검증 후 게임을 종료하고 TranslationDelayMs=0, TraceDisplay=false, TraceCatalog=false로 복원했다.
  이미지와 시나리오 기능은 Enabled=true이며 동봉 사전 설정을 유지한다.

정상 DLL SHA-256:
`43480d10e0fb9d7623212dcdbe88a3902cd74bfb833ca0d0100dc6a4ea5ee9c6`

Core DLL SHA-256:
`3204e77fbecd4d9ee2a36dcda168744630c3fef927f68249fdf8fc487631e459`

검수 시나리오 JSON SHA-256:
`7c16f0cf6b5712704b26ba913ff314b87ac7af2cdff86dc8fd2f2588b7a31594`

로컬 증거는 작업 공간의 `evidence/scenario-20261118`에 보존한다.
`diagnostic-live.log`, `reviewed-first.png`, `reviewed-voiced.png`, `unreviewed-warm-fallback.png`,
`restored-source-with-fallback.png`, `reviewed-after-enable.png`, `reviewed-speaker.png`,
`reviewed-interval-complete.png`, `reviewed-backlog.png`, `final-live.log`,
`final-reviewed-interval.png`, `final-mixed-page.png`, `final-inventory-cache-audit.json`, 빌드 및 검사 로그가 해당한다.
실기에서 네온 스토리 첫 열람에 따른 원석 50개가 자동 지급됐다. 재화 소비와 소환은 하지 않았다.

이번 단계의 실기는 표의 대표 시나리오를 대상으로 한다. 실제 Next 혼합 페이지의 native 결과와
진단 지연 서비스 검사는 별도 근거다. 모든 시나리오 및 기존 7개 회귀 조건 전체의 실기 재검증은 수행하지 않았다.
검수 파일 형식과 사용법은 [scenarios.md](scenarios.md)에 있다.
