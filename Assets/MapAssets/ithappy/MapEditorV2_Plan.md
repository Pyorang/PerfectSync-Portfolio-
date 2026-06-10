# Map Editor V2 — 직관적 에디터 설계 계획서

## 현재 에디터(V1)의 문제점 분석

| 문제 | 원인 | 체감 |
|------|------|------|
| 블록이 와이어프레임 박스로만 미리보기됨 | 고스트가 Bounds 박스만 그림 | 어떤 블록을 놓는지 안 보임 |
| 그리드에 강제 스냅만 가능 | 자유 배치 모드 없음 | 세밀한 조정 불가 |
| 배치 후 이동/회전이 불편 | 유니티 기본 Transform에 의존 | 배치 후 수정이 번거로움 |
| 블록을 하나씩만 선택 가능 | 멀티 셀렉트 없음 | 구간 통째로 이동 불가 |
| 블록 위에 블록 쌓기가 어려움 | Y 레벨 수동 조정 필요 | 표면 스냅이 없음 |
| 장애물 동작 설정이 직관적이지 않음 | 스크립트 컴포넌트 수동 추가 | 동작 미리보기 불가 |

---

## V2 설계 목표

**"선택 → 놓기 → 조정"이 3초 안에 끝나는 에디터**

핵심 원칙:
1. **보이는 대로 놓는다** — 실제 메시 고스트 프리뷰
2. **놓으면 딱 붙는다** — 표면 스냅 (블록 위에 자연스럽게 쌓기)
3. **놓은 후에도 쉽게 고친다** — 커스텀 3D 기즈모
4. **여러 개를 한번에 다룬다** — 멀티 셀렉트 + 그룹 이동
5. **동작을 눈으로 확인한다** — 장애물 동작 에디터 내 미리보기

---

## 에셋 호환성 분석

### 현재 에셋 인벤토리 (Platformer_2_Obstacles)

| 카테고리 | 수량 | 특성 | V2 에디터 대응 |
|----------|------|------|----------------|
| **Platforms** (62) | platform 001~030, box, bridge, road, slide, arch, color_box, water_platform | 바닥/길 역할. 크기가 다양함 | 표면 스냅의 주요 타겟. "놓이는 곳" |
| **Obstacles** (63) | obstacle 1~26, trampoline_plasma, tower | 대부분 애니메이션 컨트롤러 보유 (Punch, Wiggle, Spring 등) | 동작 프리셋 시스템 필요 |
| **Walls** (40) | wall 1~15, tower 1~15, arch, unicorn | 벽/기둥. 플랫폼 가장자리에 배치 | 엣지 스냅 (플랫폼 테두리에 자동 정렬) |
| **Tubes** (13) | tube 1~8, spiral 1~4 | 원통형. 입구끼리 연결 | 커넥터 포인트 (입구/출구 자동 맞춤) |
| **Interaction** (22) | coin, key, star, trampoline 1~6, bomb, apple | 수집/상호작용 아이템 | 표면 위에 떠있게 배치 (오프셋 자동) |
| **Aircraft** (28) | airship, drone 1~3, indicator | 하늘 장식. 고정 높이 배치 | Y 레벨 무관하게 자유 배치 |
| **Props** (9) | checkpoint, finish, flag, leaderboard, water | 맵 구조 필수 요소 | Start/Finish 자동 검증 |

### 기존 애니메이션 에셋

| 컨트롤러 | 용도 | V2 연동 |
|----------|------|---------|
| Punch.controller | 펀치 장애물 | "펀치" 동작 프리셋 |
| Wiggle.controller | 좌우 흔들림 | "흔들기" 동작 프리셋 |
| Spring.controller | 스프링 튕김 | "튕기기" 동작 프리셋 |
| Ball.controller | 공 굴러감 | "굴리기" 동작 프리셋 |
| Drone_Animation.controller | 드론 비행 | 자동 부착 |
| Emission_Animation.controller | 발광 효과 | "발광" 이펙트 프리셋 |

### 기존 스크립트와 V2 연동

| 스크립트 | V2에서의 활용 |
|----------|--------------|
| OscillatePosition | "왕복 이동" 프리셋 — moveAxis, moveDistance, duration을 시각적 슬라이더로 |
| OscillateRotation | "왕복 회전" 프리셋 — rotationAxis, rotationAngle을 시각적으로 |
| OscillateScale | "크기 펄스" 프리셋 — 거의 사용 안 할 듯, 옵션으로만 |
| RotationScript | "무한 회전" 프리셋 — 축/속도만 선택 |
| BlendShapeAnimator | 자동 감지 (프리팹에 SkinnedMeshRenderer 있으면) |
| Rnd_Animation | 자동 감지 (Animator 있으면) |

### 호환성 결론

에셋은 V2와 **완전 호환** 가능합니다. 프리팹 자체를 수정할 필요가 없고, 에디터 시스템만 새로 만들면 됩니다. 특히 이 에셋의 강점인 애니메이션 컨트롤러들을 "동작 프리셋"으로 시각화하면 큰 직관성 향상이 됩니다.

---

## V2 시스템 아키텍처

```
MapEditorV2/
├── Editor/
│   ├── Core/
│   │   ├── MapEditorV2Window.cs        # 메인 에디터 윈도우
│   │   ├── SceneInteraction.cs         # Scene View 모든 입력 처리
│   │   └── EditorState.cs              # 에디터 상태 머신 (Idle/Place/Move/Select)
│   │
│   ├── Placement/
│   │   ├── GhostPreview.cs             # 실제 메시 반투명 프리뷰
│   │   ├── SurfaceSnap.cs              # 표면 스냅 (레이캐스트 기반)
│   │   ├── GridSnap.cs                 # 그리드 스냅 (토글 가능)
│   │   └── PlacementValidator.cs       # 충돌/겹침 검증
│   │
│   ├── Manipulation/
│   │   ├── BlockGizmo.cs               # 커스텀 이동/회전/스케일 기즈모
│   │   ├── MultiSelect.cs             # 다중 선택 (박스 셀렉트/Ctrl+클릭)
│   │   └── GroupTransform.cs           # 그룹 이동/회전
│   │
│   ├── Behavior/
│   │   ├── BehaviorPresetPanel.cs      # 동작 프리셋 UI 패널
│   │   ├── BehaviorPreview.cs          # 에디터 내 동작 미리보기 (재생/정지)
│   │   └── BehaviorPresets.cs          # 프리셋 데이터 (SO)
│   │
│   └── UI/
│       ├── BlockBrowserPanel.cs        # 비주얼 블록 브라우저
│       ├── ToolbarOverlay.cs           # Scene View 상단 도구 모음
│       └── PropertiesPanel.cs          # 선택된 블록 속성 패널
│
├── Runtime/                            # V1과 공유 가능
│   ├── Data/
│   │   ├── BlockDefinition.cs          # V1 재사용
│   │   ├── BlockDatabase.cs            # V1 재사용
│   │   └── MapData.cs                  # V1 재사용 + 확장
│   ├── Components/
│   │   ├── MapBlock.cs                 # V1 재사용 + 확장
│   │   └── MapRoot.cs                  # V1 재사용
│   └── Serialization/
│       └── MapSerializer.cs            # V1 재사용
│
└── Resources/
    └── BehaviorPresets.asset           # 동작 프리셋 DB
```

---

## 핵심 기능 상세 설계

### 1. 실제 메시 고스트 프리뷰 (GhostPreview.cs)

**현재 (V1):** 와이어프레임 박스만 표시
**V2:** 실제 프리팹의 메시를 반투명 하늘색으로 렌더링

```
작동 방식:
1. 블록 선택 시 프리팹을 Instantiate (HideFlags.HideAndDontSave)
2. 모든 Renderer의 material을 반투명 쉐이더로 교체
3. Collider 모두 비활성화 (레이캐스트에 간섭 방지)
4. 마우스 위치 따라 실시간 이동
5. 배치 또는 취소 시 DestroyImmediate
```

유효한 위치: 하늘색 반투명 (RGBA 0.3, 0.8, 1.0, 0.4)
유효하지 않은 위치 (충돌): 빨간색 반투명 (RGBA 1.0, 0.3, 0.3, 0.4)

### 2. 표면 스냅 (SurfaceSnap.cs)

**가장 큰 직관성 개선 포인트.** 마우스가 가리키는 곳의 표면 위에 블록이 자동으로 놓임.

```
작동 방식:
1. 마우스 위치에서 Ray 발사
2. Physics.Raycast로 기존 블록의 Collider에 히트
3. 히트한 표면의 법선(normal) 방향을 파악
   - 윗면(normal ≈ up) → 블록을 위에 쌓기
   - 옆면(normal ≈ right/forward) → 블록을 옆에 붙이기
4. 히트 포인트 + 법선 방향 × 블록 크기/2 = 최종 배치 위치
5. 기존 그리드 스냅도 토글로 추가 적용 가능

히트 없을 때 (빈 공간):
→ Y=0 평면에 배치 (V1과 동일한 폴백)
```

**에셋 호환성:** 프리팹에 Collider가 있어야 작동. 에셋의 Prefab들은 대부분 MeshCollider 또는 BoxCollider를 포함하고 있음. 없는 경우 자동으로 BoxCollider를 바운드 기반으로 추가하는 옵션 제공.

### 3. 에디터 상태 머신 (EditorState.cs)

```
┌─────────┐   블록 선택    ┌──────────┐   좌클릭    ┌──────────┐
│  Idle   │ ────────────→ │ Placing  │ ──────────→ │  Idle    │
│         │               │ (고스트)  │             │(배치완료) │
└─────────┘               └──────────┘             └──────────┘
     │                         │
     │ 배치된 블록 클릭          │ ESC / 우클릭
     ▼                         ▼
┌──────────┐              ┌──────────┐
│ Selected │              │  Idle    │
│(기즈모표시)│              │ (취소)   │
└──────────┘              └──────────┘
     │
     │ G키 (잡기)
     ▼
┌──────────┐
│ Moving   │ ← 기존 블록을 잡고 이동 (고스트처럼 반투명으로)
│(이동 중)  │   좌클릭으로 놓기, ESC로 원래 위치 복귀
└──────────┘
```

상태별 입력 처리:

| 상태 | 좌클릭 | 우클릭 | R | G | Delete | Ctrl+D | Ctrl+클릭 |
|------|--------|--------|---|---|--------|--------|-----------|
| Idle | 블록 선택 | 컨텍스트 메뉴 | - | - | - | - | - |
| Placing | 배치 | 취소 | 회전 | - | - | - | - |
| Selected | 다른 블록 선택 | 선택 해제 | 회전 | 잡기(이동) | 삭제 | 복제 | 추가 선택 |
| Moving | 놓기 | 원래 위치 복귀 | 회전 | - | - | - | - |

### 4. 커스텀 블록 기즈모 (BlockGizmo.cs)

배치된 블록을 선택하면 유니티 기본 기즈모 대신 커스텀 기즈모가 표시됨.

```
           ↑ Y (높이)
           │
     ←─────●─────→ X
           │
           ↓ Z

   [↻] 회전 링 (Y축 기준, 15° 스냅)

   [□ □ □ □] 하단 도구 바
    이동 회전 복제 삭제
```

- **이동 화살표**: 3축 화살표. 드래그하면 해당 축으로만 이동. 표면 스냅 적용.
- **회전 링**: Y축 기준 원형 핸들. 드래그하면 15° 단위 스냅 회전.
- **기즈모 크기**: 카메라 거리에 따라 일정 크기 유지 (HandleUtility.GetHandleSize)

### 5. 멀티 셀렉트 (MultiSelect.cs)

```
선택 방법:
1. Ctrl + 좌클릭 → 개별 추가/제거
2. 마우스 드래그 (Idle 상태) → 박스 셀렉트
3. Ctrl + A → 전체 선택

선택된 블록들:
- 노란색 아웃라인 표시
- 중심점에 그룹 기즈모 표시
- G 키로 그룹 이동
- R 키로 그룹 회전 (중심점 기준)
- Delete로 일괄 삭제
- Ctrl+D로 일괄 복제
```

### 6. 동작 프리셋 패널 (BehaviorPresetPanel.cs)

블록 선택 후 인스펙터에 "동작 추가" 패널이 표시됨.

```
┌─ 동작 프리셋 ─────────────────────────┐
│                                        │
│  [▶ 왕복 이동]  축: [X ▼]  거리: [2.0] │
│                 속도: [──●──]          │
│                                        │
│  [▶ 무한 회전]  축: [Y ▼]  속도: [50]  │
│                                        │
│  [▶ 펀치]      세기: [──●──]          │
│                                        │
│  [▶ 흔들기]    각도: [45]  속도: [1.0] │
│                                        │
│  [▶ 튕기기]    높이: [──●──]          │
│                                        │
│  ────────────────────────────────────  │
│  [🔴 미리보기 재생]  [⏹ 정지]          │
│                                        │
│  ▶ 재생 시 에디터에서 애니메이션 실행    │
│    (EditorApplication.update로 시뮬레이션)│
└────────────────────────────────────────┘
```

**미리보기 재생:** EditorApplication.update 콜백에서 Time.realtimeSinceStartup 기반으로 OscillatePosition/RotationScript의 동작을 시뮬레이션. 실제 Play 모드에 들어가지 않고도 장애물 움직임을 확인 가능.

### 7. Scene View 도구 모음 (ToolbarOverlay.cs)

Scene View 상단에 아이콘 도구 모음이 표시됨.

```
┌──────────────────────────────────────────────────────┐
│ [📌 배치] [✋ 선택] [🔲 그리드] [🧲 표면스냅] [📏 스냅간격:2.0] │
│ [↺ Undo] [↻ Redo] [💾 저장] [📂 불러오기]                   │
└──────────────────────────────────────────────────────┘
```

현재 도구 상태가 아이콘 하이라이트로 표시. 그리드/표면스냅은 독립 토글.

---

## V1 → V2 마이그레이션

### 재사용하는 V1 코드

| V1 파일 | V2에서 | 변경사항 |
|---------|--------|----------|
| BlockDefinition.cs | 그대로 사용 | 없음 |
| BlockDatabase.cs | 그대로 사용 | 없음 |
| BlockCategory.cs | 그대로 사용 | 없음 |
| MapData.cs | 확장 | behaviorData 필드 추가 |
| MapBlock.cs | 확장 | BehaviorPreset 참조 추가 |
| MapRoot.cs | 그대로 사용 | 없음 |
| MapSerializer.cs | 그대로 사용 | 없음 |
| BlockDatabaseGenerator.cs | 그대로 사용 | 없음 |
| GridSnapSystem.cs | SurfaceSnap 내부로 통합 | 리팩터링 |

### 새로 만드는 V2 코드

| 파일 | 줄 수 (추정) | 핵심 역할 |
|------|-------------|-----------|
| EditorState.cs | ~150 | 상태 머신 |
| GhostPreview.cs | ~200 | 실제 메시 프리뷰 |
| SurfaceSnap.cs | ~250 | 표면 스냅 + 그리드 스냅 통합 |
| PlacementValidator.cs | ~100 | 충돌 검증 |
| BlockGizmo.cs | ~300 | 커스텀 3D 기즈모 |
| MultiSelect.cs | ~200 | 다중 선택 |
| GroupTransform.cs | ~150 | 그룹 이동/회전 |
| SceneInteraction.cs | ~350 | 모든 Scene 입력 처리 |
| MapEditorV2Window.cs | ~400 | 메인 윈도우 |
| BlockBrowserPanel.cs | ~200 | 블록 브라우저 |
| ToolbarOverlay.cs | ~150 | Scene 도구 모음 |
| BehaviorPresetPanel.cs | ~250 | 동작 프리셋 UI |
| BehaviorPreview.cs | ~200 | 에디터 내 동작 재생 |
| BehaviorPresets.cs | ~100 | 프리셋 SO |
| PropertiesPanel.cs | ~150 | 속성 패널 |

**총 약 15개 파일, ~3,150줄 추정**

---

## 구현 우선순위

### Phase 1: 핵심 배치 경험 (가장 큰 직관성 향상)
1. EditorState.cs — 상태 머신
2. GhostPreview.cs — 실제 메시 프리뷰
3. SurfaceSnap.cs — 표면 스냅
4. SceneInteraction.cs — 입력 통합
5. MapEditorV2Window.cs — 기본 윈도우

→ 이것만으로도 "보이는 대로 놓기 + 표면 스냅"이 가능해져서 체감 향상이 큼

### Phase 2: 배치 후 조작
6. BlockGizmo.cs — 커스텀 기즈모
7. MultiSelect.cs — 다중 선택
8. GroupTransform.cs — 그룹 이동

### Phase 3: 동작 시스템
9. BehaviorPresets.cs — 프리셋 데이터
10. BehaviorPresetPanel.cs — UI
11. BehaviorPreview.cs — 에디터 내 재생

### Phase 4: 마무리
12. BlockBrowserPanel.cs — 비주얼 브라우저
13. ToolbarOverlay.cs — 도구 모음
14. PropertiesPanel.cs — 속성 패널
15. PlacementValidator.cs — 검증

---

## 현재 에셋으로 추가 필요한 작업

### 필수
- **Collider 확인**: 모든 프리팹에 Collider가 있는지 확인 (표면 스냅에 필요). 없으면 BlockDatabaseGenerator에서 자동 BoxCollider 추가 옵션.

### 선택
- **커넥터 포인트 정의**: Tube 프리팹의 입구/출구 위치를 BlockDefinition에 수동 지정하면 자동 연결 가능. 지금은 불필요하고, Phase 1 이후 필요 시 추가.
- **동작 프리셋 DB**: 기존 Animation Controller와 스크립트를 매핑하는 ScriptableObject. Phase 3에서 작업.
