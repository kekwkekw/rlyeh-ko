# 로컬 검수 시나리오 적용

v0.2.0부터 포함한다. 외부 시나리오 CDN이나 번역 API를 사용하지 않는다.
플러그인 옆 `scenario/reviewed.json`을 게임 시작 때 읽는다.

## 적용 순서

1. `AdvImportScenarioSheet.CreateCommandList` 실행 전에 검수된 UTAGE 행의 `Text` 셀을 바꾼다.
2. UTAGE가 한국어 행으로 명령과 TextData를 만들고 기존 타이핑·음성·페이지 제어를 수행한다.
3. 표시 어댑터는 검수본을 캐시나 hdTrans에 보내지 않는다.
4. 검수본이 없는 일본어만 기존 캐시·로컬 hdTrans 경로를 사용한다.

한 페이지에 검수 행과 미번역 행이 함께 이어지면 현재 페이지의 native 명령 순서로 구간을 나눈다.
검수 구간은 그대로 보존하고 미번역 행만 번역한 뒤 같은 순서로 합친다. 검수 행이 없는 페이지는
종전과 같이 페이지 전체를 한 번 요청한다. Rich Text 때문에 요청이 글자나 태그 단위로 나뉘지 않는다.
태그 속성의 일본어 식별자는 표시 언어 판단에서 제외한다.

따라서 오래된 일본어 원문 캐시가 남아 있어도 검수본보다 우선할 수 없다.
검수본을 기존 캐시에 기록하거나 기존 캐시를 지울 필요도 없다.
페이지가 바뀌면 기존 RevisionSlot 검사로 늦은 비동기 결과를 거부한다.

페이지에 적용된 검수 행이 native 표시 문자열과 대응하지 않으면 그 페이지의 현재 텍스트를 보존하고
경고를 기록한다. 검수본을 포함한 전체 페이지를 다시 번역하는 방식으로 우회하지 않는다.

## 행 식별과 원문 보존

데이터는 정확한 grid 이름, 물리 행 위치, serialized RowIndex, 헤더와 원문 셀 전체를 보관한다.
같은 일본어라도 다른 행에는 별도의 번역을 넣을 수 있다. 일본어 문장만 검색해 다른 곳에 적용하지 않는다.
원문 또는 이미 적용된 검수본에만 대응하며 음성·화자 ID 등 다른 셀이 달라졌으면 적용하지 않는다.
번역에서 태그 인자·줄바꿈·변수·이스케이프를 바꾸면 파일을 거부하고 기존 hdTrans 경로를 유지한다.

변경하는 곳은 메모리에 올라온 `Text` 셀 하나뿐이다. `Arg1`, `Voice`, `PageCtrl`, 분기·연출 명령,
원본 book/번들은 유지한다. 문자열은 native Array.SetValue를 통해 IL2CPP의 GC write barrier를 거쳐 쓴다.

원문은 카탈로그의 `sourceCells`에 그대로 남는다. `ScenarioLocalizer.SetEnabled(false)` 이후의 native
대사 명령 실행은 해당 원문 셀을 복원한다. 이미 만들어진 명령을 재사용하는 경우도 이를 위해
`AdvCommandText.DoCommand` 진입에서 같은 정책을 확인한다. 원본 객체를 별도로 붙잡아 두지는 않는다.

이 지점은 F2 전체 전환과 연결돼 있다. F2 OFF에서는 기존 hdTrans 경로도 함께 중지한다.
현재 화면의 TextData나 과거 백로그를 즉시 다시 만드는 기능도 포함하지 않는다.
`[Scenarios] Enabled = false`는 검수 행 치환만 끄며 기존 hdTrans는 계속 동작한다.
전체 일본어 모드는 [F2 전환 안내](toggle.md)에 따라 텍스트·이미지 기능과 함께 제어한다.

## 검수 데이터 만들기

저장소의 `data/scenario/reviewed.json`이 설치 때 `scenario/reviewed.json`으로 들어간다.
초기 데이터는 직접 문맥·용어·제어 토큰을 확인한 7개 행뿐이다. 프롤로그 5행과
체이시 스토리의 interval 대사 2행이며 reviewer는 `codex-2026-11-18`이다.
전체 시나리오 번역이나 사용자 직접 검수 완료를 의미하지 않는다.

기존 추출 도구가 보존한 `importGridList` 트리로 검수 초안을 만든다.

```powershell
python -X utf8 tools/scenario.py draft <추출한-book.json.gz> --output data/local/story-review.json
```

각 항목의 `target`에 번역을 넣고 확인을 마친 항목만 `reviewer`를 채운다.
`grid`, `row`, `serializedRow`, `headerRow`, `columns`, `sourceCells`는 수정하지 않는다.
검수 파일 여러 개를 모아 컴파일할 수 있다. target/reviewer가 모두 채워진 항목만 포함한다.
기존 검수 항목을 유지하려면 기존 파일도 입력에 포함한다.

```powershell
python -X utf8 tools/scenario.py compile data/scenario/reviewed.json data/local/story-review.json
```

컴파일은 런타임과 같은 C# 검증기를 실행한다. 중복 행, 원문·태그 오류가 있으면 출력 파일을 바꾸지 않는다.
.NET SDK와 CoreChecks의 복원된 의존성이 필요하며 게임 interop은 필요하지 않다.
완성된 파일은 플러그인의 `scenario/reviewed.json`으로 복사하고 게임을 재시작한다.
캐시는 삭제하지 않는다. 초안과 전체 원본 추출물은 공개 소스 및 설치 패키지에서 제외한다.

## 참고 및 진단

[monmusu Hooks.cs](https://github.com/kekwkekw/monmusu-td-ko/blob/main/plugin/MonmusuMod/Hooks.cs)의
명령 생성 전 Text 셀 치환과 원문 행 대조, native 배열 쓰기를 참고했다.
사신전기는 로컬 단일 카탈로그를 사용하고 기존 hdTrans 표시 어댑터에 검수본 보호를 연결했다.
monmusu의 CDN 준비·세션 대기 구조는 필요하지 않아 도입하지 않았다.

`SCENARIO applied/restored` 로그는 실제 셀 쓰기를 기록한다.
`TraceDisplay=true`의 `STORY begin`은 UTAGE에 들어온 본문,
`STORY route`는 검수 행 수와 hdTrans 처리 구간 수를 기록한다.
`ScenarioProbe=true` 진단 빌드의 F9은 행 정책만 전환하는 원문 복원 검사에 사용한다.
정상 빌드는 F9 코드를 포함하지 않는다.
