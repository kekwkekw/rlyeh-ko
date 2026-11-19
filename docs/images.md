# 로컬 이미지 한글화

이미지 기능은 v0.2.0부터 포함합니다.
플러그인 옆 `images/catalog.json`과 `images/files/<SHA-256>.png`를 읽습니다.
번역 이미지도 설치 패키지에 동봉하며 런타임에서 원격 이미지를 받지 않습니다.

2026-11-20에는 사용자가 전달한 최종 PNG 19장을 통합했습니다.
[최종 이미지 이력](artwork/image-final-20261120.json)에 전달 ZIP의 해시와 이름·테마 범위별 대응을 기록했습니다.
이 통합은 PNG를 바이트 그대로 복사하며 재생성·리사이즈·색공간/알파 변경을 하지 않았습니다.
기존 19장 중 14장이 변경됐고 아포칼립스 진입 제목 5장은 이전 파일과 같습니다.
아래 제작 설명과 2026-11-18 프롬프트는 최초 제작 이력이며, 현재 최종 파일은 이 인계본입니다.
현재 적용본의 확인 범위와 화면 결과는 [2026-11-20 검증 기록](image-validation-20261120.md)에 있습니다.

## 확인한 표시 구조

| 대상 | 표시 구조 | 원본 캔버스 |
|---|---|---|
| 타이틀 로고 | TitlePage의 Image, Title_Logo_Game_R18 Sprite | 1032×456 |
| 로딩 캐릭터 소개 01~04 | GameStartLoadingScreen의 ForeImage/BackImage, Sprite 교대 표시 | 2048×920 |
| 방어 성공/실패 | 결과 연출과 결과 페이지의 Image, 본문과 가산광 Sprite 분리 | 536×156 |
| 아포칼립스 테마 카드 | RogueThemeSelectContent.m_titleImage | 588×264 |
| 테마 진입 후 제목 | RogueThemeTopPage.m_logoImage, 배경 그림 위 별도 Sprite | 800×400 |

아포칼립스 선택 화면의 `theme_banner`는 배경 삽화입니다. 일본어 제목은 카드의
`theme_title`과 진입 화면의 `theme_logo`에 들어 있습니다. 다섯 테마가 같은 Sprite 이름과
크기를 공유하므로 Setup에 전달된 RogueThemeModel.Id를 `rogue:<ID>` 범위로 사용합니다.
원본 참조는 Sprite 지정 시 보관하고, 기본 Sprite와 overrideSprite를 각각 관리합니다.
원래 override가 없으면 복원 시에도 null로 돌립니다.

로딩 소개는 예상한 세 명에 더해 시젤=메까지 네 명이 실제로 표시됩니다. 네 장 모두 포함했습니다.

## 치환과 복원

Unity UI Image의 sprite/overrideSprite 지정 및 OnEnable에서 치환합니다.
RawImage는 전체 UV의 texture 지정과 활성화 경로를 지원하며, 이번 대상은 Image입니다.
새 Texture와 Sprite를 생성하고 원본의 rect 크기, pivot, border, pixelsPerUnit을 적용합니다.
공유 원본 텍스처와 원본 번들은 보존합니다.

ImageLocalizer.SetEnabled는 F2 전체 전환에서 호출하는 진입점입니다.
비활성화하면 보관한 원본 참조를 적용하고, 활성화하면 해당 원본과 범위에서 다시 치환합니다.
게임에서 새 이미지를 지정하면 보관 원본도 갱신하며, 파괴된 Unity 객체는 캐시에서 재사용하지 않습니다.
F2 전체 전환의 설정과 반영 시점은 [toggle.md](toggle.md)에 있습니다.

`[Images] Enabled = true`가 기본입니다. false로 설정하고 게임을 재시작하면 원본 이미지를 사용합니다.

## 이미지 데이터 제작

원본 Sprite.rect와 atlas의 textureRectOffset을 기록한 뒤 편집합니다.
추출 PNG가 잘린 크기일 수 있으므로 이 크기로 native 대상을 등록하면 안 됩니다.
imagegen으로 한국어 편집을 만들고 prepare-images.ps1로 원래 캔버스에 맞춥니다.
소개 이미지는 글자 영역만 합성하고 가장자리를 12px 혼합합니다. 영역 밖 픽셀은 원본과 동일합니다.
편집 영역 안의 배경과 장식은 생성형 재현이 포함됩니다. 로고와 테마 카드는 한국어 재현 이미지입니다.
승패 본문에는 2px 흰색 외곽선을 보완하고, 같은 alpha로 흰색 가산광 이미지를 만듭니다.
정확한 제작 프롬프트는 artwork/image-generation-20261118.json에 있습니다.

```powershell
pwsh -File tools/prepare-images.ps1 -PlanFile <local-plan.json> -OutputDirectory data/images
python -X utf8 tools/image_data.py
```

로컬 plan의 assets 항목은 name, scope, width, height, edited 경로를 가집니다.
선택적인 contentRect는 원래 투명 여백을 유지할 [x,y,width,height]입니다.
regions가 있으면 original 경로의 원본 위에 지정한 사각형만 합성합니다.
whiteMask는 본문과 같은 alpha의 효과 이미지를 만들며, outline은 흰색 외곽선 반경입니다.
개인 원본 경로와 생성 중간물은 공개 소스에서 제외합니다.

카탈로그는 PNG 해시·바이트 수·원본 캔버스 크기를 검사하고 중복 대상을 거부합니다.
package.py는 검증한 이미지 목록을 소스 ZIP과 설치 payload 양쪽에 포함합니다.

## 참고와 검사

gctrans의 ImagePatch/ImageCatalog를 참고해 Unity UI 콜백에서 참조를 바꾸는 구조와
Sprite 메타데이터, 파괴된 Texture 재생성을 적용했습니다.
사신전기에는 테마 범위와 컴포넌트별 원본 보관·복원을 추가했습니다.

- 참고 revision은 dc57a9cdc4c756e334e76aa8fbebf23211cd7cac입니다.
- 원본 위치는 gctrans/source/GCMod.Candidate/GCMod.Patches/ImagePatch.cs와 GCMod.Services/ImageCatalog.cs입니다.
- CoreChecks는 같은 이름의 다른 테마, 중복 대상, 해시와 크기 불일치, 경로 이탈을 검사합니다.
- ImageProbe=true 진단 빌드는 실제 Image 경로와 Sprite 정보를 기록합니다. 진단의 F8은 이미지 참조 복원 검사용입니다.
- 정상 빌드는 진단 탐색과 F8 코드를 포함하지 않습니다.
