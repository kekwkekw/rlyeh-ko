# 로컬 시나리오 용어집

용어의 원본은 `data/canonical/glossary.json`이다. 검수 기록과 원문 근거 ID를 함께 보관하고
`tools/glossary.py export`로 hdTrans용 파일을 만든다. 개별 이름·용어를 플러그인 C#에 넣지 않는다.
2026-11-18 확장 내용은 [첫 검수 기록](glossary-validation-20261118.md),
추가 142개와 확장 중단 판단은 [2026-11-19 검수 기록](glossary-validation-20261119.md)에 있다.

## 적용 범위

| 내보내기 | 역할 |
|---|---|
| `names.txt` | 실제 인명·별칭·이표기, 지명·단체명. hdTrans가 조사와 경칭을 처리한다 |
| `userdict.txt` | 계승자·은의 열쇠·무풍기 등 작품의 용어 |
| `exact.json` | 화자명처럼 적용 범위가 제한된 정확 일치 표기 |
| `manifest.json` | canonical revision과 위 세 파일의 해시 |
| `text-catalog.json` | 별도 검수한 Master/UI 문장집. 게임의 계산 수치를 유지한다 |

인명 목록은 `speaker` 정확 일치도 생성한다. 화자명은 즉시 표시할 수 있고,
본문 속 이름은 hdTrans가 처리한다. 일반 단어·말버릇과 겹치는 표기는 화자 범위에만 둔다.
예를 들어 `スー`는 화자 `수`로만 고정하며 숨소리나 `スープ`에는 적용하지 않는다.
의상·표정·크기용 내부 ID를 표시 이름으로 사전에 넣지 않는다.

시나리오의 적용 순서는 **검수된 UTAGE 행 → 기존 캐시/로컬 hdTrans**이다.
용어집을 확장해도 검수 행을 다시 번역하지 않으며 [시나리오 우선 치환](scenarios.md)을 유지한다.
F2 OFF에서는 화자 정확 일치와 본문 번역을 함께 중지한다.

## 검수와 업데이트

신규 용어는 실제 표시명, 본문의 반복 사용·정의·자기소개를 확인한다.
`kana` 보조 필드와 표시명이 다르면 표시 원문을 별도로 대조하고 판단 근거를 남긴다.
이름의 표기 선택을 공식 한국어 이름으로 주장하지 않으며, Codex 검수와 사용자 직접 검수를 구분한다.

`names`/`userdict`에 전역 등록할 항목과 `speaker`/`ui` 정확 일치를 구분한다.
짧은 이름은 더 긴 일반 단어·다른 이름을 깨뜨리지 않는지 실제 hdTrans로 비교한다.
일반 문법 문제를 전체 문장 사전이나 새 엔진 규칙으로 덮지 않는다.
`深きもの`는 단독 입력의 정확 일치만 허용한다. 전역 치환하면 일반적인 "의미가 깊은 것"도 바뀐다.
`アリス`의 전역 등록도 고전 동화 제목까지 바꾸므로 보류한다. 인물 호칭 `にゃー様`는 이름으로
분류해 사람 대상 조사를 유지하며, 일반 고양이 울음 `にゃー`에는 적용하지 않는다.

로컬 원문 추출물과 review pack이 있으면 기존 `tools/glossary.py import`의 근거 해시 검사를 사용한다.
이미 검수한 canonical을 수정할 때에도 reviewer, revision, 적용 범위와 근거를 함께 갱신한다.

```powershell
python -X utf8 tools/glossary.py export
python -X utf8 -m unittest discover -s tests -p 'test_*.py'
dotnet run --project tests/CoreChecks -c Release
```

실제 동봉 hdTrans 검사에는 추가 데이터가 있다. 게임을 종료하고 다음처럼 실행한다.

```powershell
dotnet run --project tests/CoreChecks -c Release -- --service <plugin-folder> <dicts.hdsnap> tests/fixtures/scenario-glossary-20261118.json
dotnet run --project tests/CoreChecks -c Release -- --service <plugin-folder> <dicts.hdsnap> tests/fixtures/scenario-glossary-20261119.json
```

설치할 때는 게임을 종료하고 `data/exports/`의 다섯 파일을 플러그인의 `glossary/`에 함께 적용한다.
플러그인은 시작 시 해당 사전을 고정하므로 적용 후 재시작한다.
사전·문장집이 바뀌면 캐시 revision도 바뀐다. 이전 캐시 파일은 보존되며 새 사전의 결과를 새 revision에 저장한다.
예전 번역값을 새 캐시로 그대로 복사하지 않는다. 번역 속도를 유지하려면 기존 캐시의 **원문**을 새 사전으로 다시 처리한다.

같은 Master 원문 템플릿은 검수 표현을 통일한다. native 수치를 넣었을 때 같은 원문 표시 키가 되는
스킬들이 서로 다른 번역을 등록하면 공통 표시 캐시가 해당 직접 매핑을 거부한다.
Python의 실제 문장집 검사와 Core의 AutoSkill 12/24 수치별 검사가 이를 확인한다.
