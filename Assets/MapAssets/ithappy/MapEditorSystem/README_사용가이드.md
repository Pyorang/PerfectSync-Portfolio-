# Platformer Map Editor — 사용 가이드

## 초기 설정 (최초 1회)

### Step 1. 유니티 프로젝트에 넣기

`MapEditorSystem` 폴더를 `Assets/` 아래에 배치합니다.
기존 `Platformer_2_Obstacles` 에셋도 `Assets/` 아래에 있어야 합니다.

```
Assets/
├── Platformer_2_Obstacles/   ← 기존 에셋
└── MapEditorSystem/          ← 이 시스템
```

### Step 2. Block Database 자동 생성

유니티 상단 메뉴에서:

```
MapEditor > Auto Generate Block Database
```

이 명령은 `Platformer_2_Obstacles/Prefabs/` 폴더를 자동으로 스캔하여
237개 프리팹을 7개 카테고리로 분류하고 BlockDefinition을 만들어줍니다.

완료 후 `Assets/MapEditorSystem/Resources/` 아래에 생성되는 파일:
- `BlockDatabase.asset` — 전체 블록 DB
- `BlockDefinitions/` — 카테고리별 개별 블록 정의 파일들

### Step 3. MapEditorSettings 설정

유니티 상단 메뉴에서:

```
MapEditor > Select Block Database
```

를 실행하면 BlockDatabase가 선택됩니다.

그 다음, 다음 중 하나로 Settings를 만듭니다:
- 자동: Map Editor 윈도우를 열면 자동 생성됨
- 수동: Project 창에서 `우클릭 > Create > MapEditor > Editor Settings`

Settings에서 **Block Database 필드에 위에서 생성한 BlockDatabase를 드래그해서 연결**합니다.

---

## 에디터 사용법

### Map Editor 윈도우 열기

```
Window > Map Editor    (단축키: Ctrl + Alt + M)
```

### 윈도우 구성

```
┌──────────────────────────────────────────────────────┐
│  [Settings 필드]                    [Find MapRoot]   │  ← 상단 툴바
├───────────┬──────────────────────────────────────────┤
│ Categories│  검색: [_______________]                  │
│ ────────  │                                          │
│  All      │  ┌────┐ ┌────┐ ┌────┐ ┌────┐            │
│  Platforms│  │    │ │    │ │    │ │    │            │
│  Obstacles│  │ 썸네│ │ 썸네│ │ 썸네│ │ 썸네│            │
│  Walls    │  │ 일  │ │ 일  │ │ 일  │ │ 일  │            │
│  Tubes    │  └────┘ └────┘ └────┘ └────┘            │  ← 블록 팔레트
│  Interact │  platform platform platform platform     │
│  Aircraft │  _001    _002    _003    _004            │
│  Props    │                                          │
├───────────┴──────────────────────────────────────────┤
│  Grid Size: [2.0]   Snap: [✓]   Show Grid: [✓]     │
│  Rotation Snap: [15]   Y Level: [0.0]               │  ← 그리드 설정
├──────────────────────────────────────────────────────┤
│  Map Name: Untitled    Block Count: 0                │
│  [Save JSON]  [Load JSON]                            │  ← 맵 관리
│  [Clear All]                                         │
└──────────────────────────────────────────────────────┘
```

---

## 블록 배치 워크플로우

### 기본 배치

1. **왼쪽 카테고리 패널**에서 원하는 카테고리를 클릭합니다
   (예: `Platforms`를 선택하면 플랫폼 블록만 표시)

2. **블록 팔레트**에서 원하는 블록의 썸네일을 **클릭**합니다
   → 배치 모드가 활성화됩니다

3. **Scene View**로 마우스를 이동하면 파란색 와이어프레임이 그리드에 스냅된 위치에 표시됩니다

4. 원하는 위치에서 **좌클릭**하면 블록이 배치됩니다

5. **우클릭**하면 배치 모드가 해제됩니다

### 배치 중 조작

| 조작 | 기능 |
|------|------|
| 좌클릭 | 현재 위치에 블록 배치 |
| 우클릭 | 배치 모드 해제 |
| R 키 | 시계방향으로 90° 회전 |
| Shift+R 또는 T 키 | 반시계방향으로 90° 회전 |
| 마우스 휠 ↑↓ | 배치 높이(Y) 한 칸씩 올리기/내리기 |
| ESC | 배치 모드 해제 |

### 연속 배치

배치 모드는 블록을 놓은 후에도 유지됩니다.
같은 블록을 여러 개 연속으로 놓으려면 계속 좌클릭하면 됩니다.
다른 블록을 놓으려면 팔레트에서 다른 블록을 클릭합니다.

---

## 블록 편집 (인스펙터)

Scene에서 배치된 블록을 선택하면 Inspector에 커스텀 UI가 표시됩니다.

### Block Info

```
Block ID: platform_001
Definition: Platform 001
Change Block: [platform_001 ▼]   ← 같은 카테고리 내 다른 블록으로 교체
```

**블록 교체**: 드롭다운에서 다른 블록을 선택하면 위치/회전/크기를 유지한 채 외형만 바뀝니다.

### Transform Controls

```
Grid Position: X[0] Y[0] Z[3]     ← 그리드 좌표로 직접 입력
[Snap to Grid]                      ← 현재 위치를 가장 가까운 그리드로 보정

Rotation (Y-axis)
[0°] [90°] [180°] [270°]          ← 원클릭 회전 프리셋
Custom Rotation: X[0] Y[90] Z[0]   ← 자유 회전

Scale
[0.5x] [1x] [1.5x] [2x]          ← 원클릭 스케일 프리셋
Custom Scale: X[1] Y[1] Z[1]       ← 자유 스케일
```

### Block Actions

```
[Duplicate →]  [Duplicate ↑]  [Duplicate →→]
```
- **→** : X축 양의 방향으로 한 칸 복제
- **↑** : Y축 위로 한 칸 복제
- **→→** : Z축 양의 방향으로 한 칸 복제

```
[Delete]                            ← 블록 삭제
```

### Animation Scripts

기존 에셋의 애니메이션 스크립트를 원클릭으로 추가합니다:

```
[+ Oscillate Position]    ← 위치 흔들림 (상하좌우 왕복)
[+ Oscillate Rotation]    ← 회전 흔들림 (좌우 흔들기)
[+ Oscillate Scale]       ← 크기 흔들림 (커졌다 작아지기)
[+ Rotation Script]       ← 무한 회전 (풍차, 프로펠러 등)
[+ Blend Shape Anim]      ← 블렌드쉐이프 애니메이션
```

이미 추가된 스크립트는 `✓ Has OscillatePosition` 으로 표시됩니다.

---

## 그리드 시스템

### 그리드 설정

Map Editor 윈도우 하단에서 조절합니다:

| 설정 | 기본값 | 설명 |
|------|--------|------|
| Grid Size | 2.0 | 그리드 한 칸의 크기 (미터) |
| Snap Enabled | ✓ | 스냅 활성화. 끄면 자유 배치 |
| Show Grid | ✓ | Scene View에 그리드 라인 표시 |
| Rotation Snap | 15 | 회전 스냅 단위 (도) |
| Y Level | 0.0 | 현재 작업 높이 |

### 그리드 시각화

Scene View에 표시되는 요소들:

- **흰색 그리드 라인** — 기본 그리드
- **하늘색 하이라이트 라인** — 5칸마다 강조 (위치 파악 용이)
- **하늘색 구체** — 현재 스냅 포인트
- **하늘색 와이어프레임 박스** — 블록 배치 프리뷰
- **노란색 와이어프레임 박스** — 선택된 블록의 바운드
- **초록 점선** — 인접 블록 간 연결선
- **빨간 반투명 사각형** — 현재 Y 레벨 표시

### HUD (Scene View 좌상단)

```
┌─────────────────────────┐
│ Map: Stage_01           │
│ Blocks: 47              │
│ Grid Size: 2            │
│ Current Y: 4.0          │
│ Snap: ON                │
└─────────────────────────┘
```

---

## 맵 저장 / 불러오기

### 저장 (JSON)

1. Map Editor 윈도우에서 **[Save JSON]** 클릭
2. 파일 저장 대화상자에서 경로와 이름 지정
3. `.json` 파일로 저장됨

### 불러오기

1. **[Load JSON]** 클릭
2. 파일 선택 대화상자에서 `.json` 파일 선택
3. 기존 블록이 모두 지워지고 저장된 맵이 로드됨

### JSON 구조 예시

```json
{
  "mapName": "Stage_01",
  "version": "1.0",
  "gridSize": 2.0,
  "createdAt": "2026-03-19T...",
  "blocks": [
    {
      "blockId": "platform_001",
      "position": { "x": 0, "y": 0, "z": 0 },
      "rotation": { "x": 0, "y": 0, "z": 0 },
      "scale": { "x": 1, "y": 1, "z": 1 },
      "sortOrder": 0
    }
  ],
  "metadata": {
    "author": "",
    "description": "",
    "difficulty": 1,
    "estimatedTime": 0
  }
}
```

### 전체 삭제

**[Clear All]** 클릭 → 확인 대화상자 표시 → "Yes" 클릭 시 모든 블록 삭제

---

## 블록 카테고리 가이드

| 카테고리 | 블록 수 | 용도 | 대표 에셋 |
|----------|---------|------|-----------|
| **Platforms** | 62개 | 플레이어가 밟고 지나가는 바닥/길 | platform, box, bridge, road, slide, water_platform, color_box, arch |
| **Obstacles** | 63개 | 플레이어를 방해하는 장애물 | obstacle 1~26 시리즈, 회전하는 바, 밀어내는 블록, trampoline_plasma |
| **Walls** | 40개 | 벽, 울타리, 기둥 | wall 1~15, tower 1~15, arch, unicorn |
| **Tubes** | 13개 | 통과하는 튜브/파이프 | tube 1~8, tube_spiral 1~4, tube_partition |
| **Interaction** | 21개 | 수집/상호작용 아이템 | coin, key, star, ring, apple, banana, bomb, trampoline 1~6 |
| **Aircraft** | 28개 | 하늘 장식/비행체 | airship, drone 1~3, indicator, propeller |
| **Props** | 9개 | 맵 장식/UI 요소 | checkpoint, finish, flag, leaderboard, water, box_tnt |

### 맵 제작 팁

**기본 구조 만들기**: Platforms 카테고리에서 `platform_001` ~ `platform_030`을 조합하여 길을 깝니다. `bridge`로 빈 공간을 연결하고, `slide`로 경사로를 만듭니다.

**장애물 배치**: Obstacles에서 장애물을 배치합니다. `RotationScript`이나 `OscillatePosition`을 추가하면 움직이는 장애물이 됩니다.

**높이 변화**: 마우스 휠로 Y Level을 올린 뒤 위층 플랫폼을 배치합니다. `tower` 블록을 기둥으로 사용하면 자연스럽습니다.

**코스 마무리**: Props에서 `checkpoint`를 중간중간 배치하고, `finish`로 결승선을 만듭니다. `flag`로 장식합니다.

**하늘 꾸미기**: Aircraft에서 `airship`이나 `drone`을 높은 Y Level에 배치하여 배경을 꾸밉니다.

---

## Undo / Redo

모든 블록 배치, 삭제, 이동, 회전, 복제는 유니티의 Undo 시스템과 완전히 통합되어 있습니다.

- **Ctrl + Z** — 되돌리기
- **Ctrl + Y** — 다시 실행

---

## 메뉴 모음

| 메뉴 경로 | 기능 |
|-----------|------|
| `Window > Map Editor` | 에디터 윈도우 열기 (Ctrl+Alt+M) |
| `MapEditor > Auto Generate Block Database` | Prefabs 스캔하여 DB 생성 |
| `MapEditor > Regenerate Thumbnails` | 블록 썸네일 재생성 |
| `MapEditor > Select Block Database` | 프로젝트에서 DB 선택 |

---

## 문제 해결

**블록 팔레트가 비어있다면:**
- Settings의 Block Database 필드가 연결되어 있는지 확인
- `MapEditor > Auto Generate Block Database` 재실행

**그리드가 안 보인다면:**
- Map Editor 윈도우에서 "Show Grid" 체크 확인
- Scene View가 활성화되어 있는지 확인

**블록이 엉뚱한 위치에 놓인다면:**
- "Snap Enabled"가 체크되어 있는지 확인
- Y Level 값 확인 (0이 기본)
- Grid Size가 적절한지 확인 (기본 2.0)

**MapRoot가 없다고 나온다면:**
- "Find MapRoot" 버튼 클릭 (자동 생성됨)
- 또는 Hierarchy에서 빈 오브젝트에 MapRoot 컴포넌트 수동 추가
