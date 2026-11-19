# 이미지 한글화 구현·검증 — 2026-11-18

공개 v0.1.0 이후의 로컬 개발 브랜치 `feat/local-images` 작업이다.
기존 텍스트 번역 런타임과 hdTrans 0.9.5를 유지하고, 이미지 참조 치환 기능을 추가했다.
공개 저장소 push, main 병합, 새 릴리스 및 설치 ZIP 생성은 하지 않았다.

## 실제 표시 구조와 데이터

| 구분 | 원본 참조 | 개수 | 캔버스 |
|---|---|---:|---|
| 타이틀 | TitlePage의 Logo Image / Title_Logo_Game_R18 | 1 | 1032×456 |
| 시작 로딩 소개 | GameStartLoadingScreen.ForeImage / BackImage, gamestart_loading_image_01~04 | 4 | 2048×920 |
| 승리·패배 | 결과 연출과 결과 페이지의 Image, 본문/가산광 분리 | 4 | 536×156 |
| 아포칼립스 테마 카드 | RogueThemeSelectContent.m_titleImage / theme_title | 5 | 588×264 |
| 테마 진입 제목 | RogueThemeTopPage.m_logoImage / theme_logo | 5 | 800×400 |

총 19개 PNG와 카탈로그 1개를 포함한다. 로딩 소개는 처음 예상한 세 명 외에
시젤=메도 실제로 표시되어 네 장으로 확장했다. 제목 글자가 없는 아포칼립스 배경 삽화
`theme_banner`는 원본을 유지하고, 그 위의 별도 제목 Sprite를 바꾼다.
같은 이름·크기의 테마 제목은 게임의 RogueThemeModel.Id로 구분한다.

gctrans revision `dc57a9cdc4c756e334e76aa8fbebf23211cd7cac`의 ImagePatch/ImageCatalog를
참고했다. 사신전기의 테마 구분과 컴포넌트별 원본 참조·복원을 새 구현에 반영했다.
기존에 동작하던 텍스트 경로 위에 이미지용 텍스트 표시층을 추가하지 않는다.

## 실기 결과

| 항목 | 결과 |
|---|---|
| 정상 빌드 시작 | BepInEx/IL2CPP 시작, 이미지 카탈로그 19개, 동봉 hdTrans 0.9.5 정상 |
| 타이틀 로고 | 한국어 표시 확인 |
| 로딩 소개 01 네온 / 02 엘레누아르 / 03 이그나 / 04 시젤=메 | 네 장 모두 정상 로딩 경로에서 한국어 표시 확인 |
| 아포칼립스 1~5 카드와 진입 제목 | 다섯 테마 모두 한국어 및 제목 대응 확인 |
| 승리 | 완료된 MS-00-03 재전투의 연출과 결과 페이지에서 확인 |
| 패배 | MS-06-15 실제 패배 결과 페이지에서 확인 |
| 이미지 원본 복원·재적용 | 진단 빌드에서 테마 카드와 테마 3 진입 제목의 한국어→원본→한국어 확인 |
| 기존 텍스트 표시 | 메뉴·확인창, 스토리 본문·화자 한글, 스토리 빠른 진행 및 화면 전환 확인 |

철수는 패배 화면을 거치지 않고 복귀하므로 패배 검증 근거로 사용하지 않았다.
승리 이미지는 초기에 약했던 대비를 2px 흰색 외곽선으로 보완한 최종 데이터로 재확인했다.
F2 전체 ON/OFF는 후속 단계이다. 이번에는 `ImageLocalizer.SetEnabled(bool)`와
컴포넌트별 원본 Sprite/overrideSprite/Texture 보관을 구현했다. 원본 번들은 수정하지 않는다.
복원 검사용 F8과 객체 탐색 기록은 `ImageProbe=true` 빌드에만 존재한다.
현재 게임에는 해당 진단 코드를 제외한 정상 빌드가 설치돼 있다.

## 검사 결과

- 정상 플러그인 빌드: 오류 0. NuGet 취약점 메타데이터 서버 접근 실패 경고 NU1900 2건.
- CoreChecks 48개 통과. 새 이미지 검사 6개는 테마 식별, 알 수 없는 대상 유지,
  중복 거부, 경로 제한, 캔버스 크기 및 손상된 PNG 해시 검증을 포함한다.
- 기존 Python 테스트 13개 통과.
- 이미지 데이터 검사: 20개 파일의 SHA-256·바이트 수·PNG 크기 검사 통과.
- 공개 소스 감사: 추적 파일 110개 통과. 공백 오류 검사 통과.
- 설치된 DLL·Core와 이미지 20개 파일이 개발 소스의 빌드 결과·배포 데이터와 일치.
- 소스 ZIP 생성 대상 목록에도 이미지 카탈로그와 PNG 19개가 모두 포함됨을 확인.
- 소개 이미지 4장: 지정한 글자 편집 영역 밖의 모든 픽셀이 추출 원본과 일치.
- 승리/패배 각각의 본문과 발광 효과 alpha 일치.
- 기존 7개 회귀 조건을 새 기능과 함께 모두 다시 실기 재현한 것은 아니다.
  텍스트 관련 CoreChecks와 위 표의 시작·스토리·메뉴 동작을 확인했다.

이미지 단계 검증 당시 정상 DLL SHA-256 (후속 시나리오 단계 빌드와 구분):
`a69ec1cd7450842e7e7078198d3aac60e3e471b3d79c7ddaae7337ce4464cd1a`

이미지 catalog.json SHA-256:
`c410329011f564a84d501a4dd0f2b4630c3a160395bedaab962a1c06a1f9f84a`

로컬 증거는 작업 공간의 `evidence/image-assets-20261118`에 보존했다.
`diagnostic-live.log`, `diagnostic-views.jsonl`, `final-live.log`, `final-loading-live.log`, `title-final-live.png`,
`intro01-final-live.png`~`intro04-final-live.png`,
`victory-animation-final-live.png`, `victory-final-live.png`, `defeat-final-live.png`,
`theme1-live.png`~`theme5-live.png`, `theme3-restored.png`, `story-final-regression.png`가 해당한다.
게임 경로·계정 화면·로그·원본 추출물은 공개 소스에 넣지 않는다.

## 이미지 제작과 후속 작업

한국어 로고와 테마 카드는 생성형 편집으로 만든 현지화 이미지다. 소개 이미지는
생성 결과 전체를 사용하지 않고 글자 영역만 원본에 합성했다. 그 영역 안의 배경과 장식에는
생성형 재현이 포함된다. 제작 프롬프트는 `artwork/image-generation-20261118.json`,
로컬 카탈로그와 재가공 방법은 `images.md`에 기록했다.

이 단계의 다음 작업은 시나리오 행 직접 치환이다. 이후 F2 전체 전환에서 이미지의
SetEnabled를 텍스트·시나리오와 함께 연결하고, 마지막으로 용어집 개선을 진행한다.
