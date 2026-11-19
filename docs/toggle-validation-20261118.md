# F2 전체 전환 구현·검증 — 2026-11-18

`feat/local-images`의 이미지·시나리오 단계에 이어 구현했다. 공개 push, 병합, 배포물 생성은 하지 않았다.
사용법과 기존 화면의 갱신 범위는 [toggle.md](toggle.md)를 참고한다.

## 변경과 실기에서 발견한 원인

- `[General] Enabled`를 F2로 바꾸고 마지막 선택을 저장한다. 별도 이미지·시나리오 설정도 존중한다.
- 하나의 세대 번호를 공유해 이미지, 시나리오 행, Master·UI, 검수본/캐시 조회와 로컬 번역 요청을 제어한다.
- OFF에서 대기 번역을 건너뛰고 이전 세대의 완료 콜백을 거부한다. ON→OFF→ON도 이전 ON의 결과를 받아들이지 않는다.
  진행 중인 native 번역 자체는 끝날 수 있으나 화면에는 적용하지 않는다. 엔진과 영속 캐시는 유지한다.
- 일반 TMP는 원래 바인딩으로 처리 버퍼를 다시 만든다. OFF 중 생성된 객체도 기록해 재활성화 시 갱신한다.
  TMP setter/주기적 탐색 경로를 추가하지 않았다. UGUI 원문과 가독성 설정은 보관한 값으로 복원한다.
- 첫 실기에서 Master 설명 일부가 OFF 후 화면 재진입에도 한국어로 남았다. IL2CPP 형식 확인 결과
  `ActiveSkillModel`, `SpecialSkillModel`, `PassiveSkillModel`은 생성 시 설명을 `Text`에 보관한다.
  따라서 `SkillData.GetText` 반환값을 번역하면 그 한국어가 로그인 동안 남을 수 있다.
- 이 반환값 치환을 제거했다. `GetText`/`GetDiffText`는 일본어를 반환하며, 검수 템플릿과 native 계산값으로
  완성한 표시 쌍을 별도 `MasterDisplayCache`에 준비한다. 공통 UI 표시 경로가 ON일 때만 이를 적용한다.
  OFF로 로그인한 후 ON해도 검수된 Master 설명이 즉시 표시된다. 원래 경로와 병행하지 않는다.

## 최종 빌드의 실기 결과

| 대상 | 확인 결과 |
|---|---|
| Unity 6000.3.5f2 / BepInEx / Interop | OFF로 재시작, hdTrans 0.9.5 준비, 일반 화면 진입 정상 |
| 저장 상태 | F2 OFF의 설정 저장과 OFF 재시작 확인, 이후 ON 복귀 |
| 타이틀·로딩 소개 | F2로 일본어 이미지 복원과 한국어 재적용 확인 |
| Master 스킬 | 체이시의 설명·소질·이름·분류·수치 UI를 같은 화면에서 한국어→일본어→한국어 확인. 수치 보존 |
| 일반 UI | 시작 안내, 타이틀, 홈, 캐릭터 목록, 도감, 아포칼립스, 소환 메뉴의 원문/한국어 전환 확인 |
| 시나리오 검수본 | `10001A1_Nml` 28행 한국어 시작, OFF 후 35행 native 원문 복원, ON 재진입 후 검수본 재적용 |
| 미검수 시나리오 | OFF의 화자명·대사 일본어 유지. ON 후 기존 hdTrans/캐시 번역과 한국어 화자명 재개 |
| 혼합 페이지 | 35행 검수본+53행 hdTrans, interval 유지, 두 줄 끝까지 표시. reviewed=1 / engineParts=1 |
| 타이핑·즉시 표시·스킵·페이지 전환 | 기존 대사 진행 및 스킵 확인창으로 정상 복귀. 끝 글자 잘림 없음 |
| 아포칼립스 이미지 | 테마 카드 1~3과 테마 1 진입 제목의 원본 복원/한국어 재적용, 소개 문구도 함께 전환 |
| 소환 확인 어댑터 | 원석 3000개/10회 확인창을 ON→OFF 재열기→ON 재열기로 확인. 필요량·보유량 불변, 모두 취소 |
| 계정 재화 | 이번 F2 검증 시작/종료 원석 값 동일. 소비·소환 없음. 실제 숫자는 비공개 증거에 보존 |

소환 확인창 본문과 현재 UTAGE 페이지는 F2 순간 기존 언어가 남을 수 있다.
창 재열기·다음 대사 또는 스토리 재진입 후 선택한 모드를 적용한다. 과거 백로그는 다시 만들지 않는다.
이는 사용자에게 허용된 화면 이동/재로딩 반영 범위다.

이번 단계는 대표 화면의 전환 검증이다. 모든 시나리오 행, 전체 Master와 모든 메뉴를 순회한 결과는 아니다.
승리·패배 이미지의 실제 표시 검증은 이미지 단계 기록을 유지하며, 이번에 전투를 재실행하지 않았다.
이전 단계 보고서의 DLL 해시는 해당 단계 당시 결과이며 아래가 현재 설치본이다.

## 자동 검사·설치 상태

- CoreChecks 기본 77개 통과. 세대 전환, 원문/검수 표시 쌍 분리와 상충 매핑 거부 검사 포함.
- Python 13개 통과.
- 실제 동봉 hdTrans 서비스 검사 통과. Master 즉시 표시 경로, OFF의 검수본/캐시/요청 차단,
  엔진 재초기화 없이 ON 재개, 지연된 이전 세대 콜백 거부를 확인했다.
- 영속 캐시 재사용 검사 통과. 캐시 키·사전 revision·hdTrans 버전은 변경하지 않았다.
- 정상 빌드 오류 0, NU1900 경고 2건(오프라인 NuGet 취약점 메타데이터 접근).
- 게임 설치 DLL과 빌드 산출물의 SHA-256 일치. 진단용 ImageProbe/ScenarioProbe 빌드 아님.
- 공개 소스 감사와 공백 오류 검사 통과. 로컬 증거는 작업 공간의 `evidence/f2-20261118`에 보존한다.
- 검증 후 게임 종료. General/Images/Scenarios Enabled=true, TranslateUi=true, Dictionary 비움,
  TranslationDelayMs=0, TraceDisplay=false, TraceCatalog=false로 남겼다.

Plugin DLL SHA-256:
`44a99bb621a102140a5b2d8aa9bbfa7f420274aff5b4314d1c68fcca54317e7a`

Core DLL SHA-256:
`4671c93d0da3a982b01ad367ef38795b16556d7d7de7400ee9c6d87ce8d15799`

주요 증거: `final-live.log`, `master-final-on.jpg`, `master-final-off.jpg`,
`story-reviewed-off.jpg`, `story-reviewed-reapplied.jpg`, `story-mixed-on.jpg`,
`apocalypse-cards-off.jpg`, `apocalypse-entry-on.jpg`, `summon-off-reloaded.jpg`,
`summon-reapplied.jpg`, `final-config.cfg`, build/core/service/warm/python 검사 로그.
타이틀·소개 이미지 전환 캡처는 Master 수정 전 동일한 F2 이미지 경로에서도 보존했다.

## 다음 용어집 단계에서 정리할 항목

AutoSkillMasterData 12와 24는 원문 템플릿이 같고, 검수본의 밀어내기 표현만
`크게 뒤로 밀어내는` / `크게 밀어내는`으로 다르다. 실제 계산값도 같으면 원문 표시 키가 겹친다.
서로 다른 검수 결과 중 하나를 임의 선택하지 않도록 이 직접 매핑은 차단하고 기존 UI 번역 경로를 사용한다.
F2 전환에는 영향이 없으며, 후속 용어집 단계에서 두 검수 표현을 통일할 항목이다.
이번에는 검수 데이터와 캐시 revision을 바꾸지 않았다.

후속 처리: [4단계 용어집 검수](glossary-validation-20261118.md)에서 두 표현을 통일했고
공통 표시 캐시의 수치별 등록·조회와 실제 게임 표시를 확인했다. 위 내용은 F2 단계 당시의 기록이다.
