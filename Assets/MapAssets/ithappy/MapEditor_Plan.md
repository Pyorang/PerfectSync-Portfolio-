# Platformer Map Editor - 설계 계획서

## 에셋 분석 요약

**Platformer_2_Obstacles** (namespace: `ithappy`)

| 카테고리 | 프리팹 수 | 주요 에셋 |
|----------|-----------|-----------|
| Platforms | 62개 | platform, box, bridge, arch, road, slide, water_platform, color_box |
| Obstacles | 63개 | obstacle 1~26 시리즈, box, tower, trampoline_plasma |
| Walls | 40개 | wall 1~15, tower 1~15, arch, ball, unicorn |
| Tubes | 13개 | tube 1~8, tube_partition, tube_spiral 1~4 |
| Interaction | 21개 | trampoline 1~6, coin, key, bomb, apple, banana, ring, star |
| Aircraft | 28개 | airship, drone 1~3, indicator, propeller |
| Props | 9개 | checkpoint, finish, flag, leaderboard, water, box_tnt |

기존 스크립트: `OscillatePosition`, `OscillateRotation`, `OscillateScale`, `RotationScript`, `BlendShapeAnimator`, `Rnd_Animation`

---

## 시스템 아키텍처 개요

```
MapEditorSystem/
├── Editor/                          # Unity Editor 전용 (Editor 폴더)
│   ├── MapEditorWindow.cs           # 메인 에디터 윈도우
│   ├── BlockPalettePanel.cs         # 블록 선택 팔레트 UI
│   ├── MapEditorSceneGUI.cs         # Scene View 오버레이 & 핸들
│   ├── BlockPlacementTool.cs        # EditorTool 기반 배치 도구
│   ├── GridSnapSystem.cs            # 그리드 스냅 로직
│   ├── BlockInspectorEditor.cs      # 배치된 블록 커스텀 인스펙터
│   └── MapEditorSettings.cs         # 에디터 설정 (ScriptableObject)
│
├── Runtime/                         # 런타임에서도 사용 가능한 코드
│   ├── MapData.cs                   # 맵 데이터 구조 (JSON 직렬화)
│   ├── MapSerializer.cs             # JSON 저장/불러오기
│   ├── BlockDefinition.cs           # 블록 메타데이터 정의
│   ├── BlockDatabase.cs             # 블록 데이터베이스 (ScriptableObject)
│   ├── MapBlock.cs                  # 배치된 블록 컴포넌트
│   └── MapRoot.cs                   # 맵 루트 오브젝트 관리
│
└── Resources/
    └── BlockDatabase.asset          # 블록 DB 에셋
```

---

## 1단계: 데이터 기반 (Runtime)

### 1-1. BlockDefinition.cs — 블록 메타데이터

각 프리팹에 대한 메타정보를 정의하는 ScriptableObject.

```csharp
[CreateAssetMenu(menuName = "MapEditor/Block Definition")]
public class BlockDefinition : ScriptableObject
{
    public string blockId;              // 고유 ID (예: "platform_001")
    public string displayName;          // 표시 이름
    public BlockCategory category;      // 카테고리 enum
    public GameObject prefab;           // 원본 프리팹 참조
    public Texture2D thumbnail;         // 팔레트용 미리보기 이미지
    public Vector3 gridSize;            // 이 블록이 차지하는 그리드 단위 크기
    public Vector3 snapOffset;          // 스냅 시 위치 보정값
    public bool allowRotation;          // 회전 허용 여부
    public float[] allowedRotations;    // 허용되는 회전 각도 (예: 0, 90, 180, 270)
    public bool allowScaling;           // 스케일 조절 허용 여부
    public Vector3 minScale;
    public Vector3 maxScale;
    public string[] tags;               // 검색용 태그
}

public enum BlockCategory
{
    Platforms, Obstacles, Walls, Tubes,
    Interaction, Aircraft, Props
}
```

### 1-2. BlockDatabase.cs — 블록 DB

모든 BlockDefinition을 한곳에서 관리.

```csharp
[CreateAssetMenu(menuName = "MapEditor/Block Database")]
public class BlockDatabase : ScriptableObject
{
    public List<BlockDefinition> blocks;

    // 카테고리별 필터링
    public List<BlockDefinition> GetByCategory(BlockCategory cat);
    // 검색
    public List<BlockDefinition> Search(string keyword);
    // ID로 찾기
    public BlockDefinition GetById(string blockId);
}
```

**자동 생성 기능**: 에디터 메뉴에서 Prefabs 폴더를 스캔하여 BlockDefinition을 자동 생성하는 유틸리티를 제공. 프리팹 이름 → blockId, 폴더명 → category 매핑.

### 1-3. MapData.cs — JSON 직렬화 구조

```csharp
[Serializable]
public class MapData
{
    public string mapName;
    public string version;              // "1.0"
    public string createdAt;            // ISO 8601
    public float gridSize;              // 사용된 그리드 크기
    public List<PlacedBlockData> blocks;
    public MapMetadata metadata;        // 난이도, 예상 시간 등
}

[Serializable]
public class PlacedBlockData
{
    public string blockId;              // BlockDefinition.blockId 참조
    public SerializableVector3 position;
    public SerializableVector3 rotation; // Euler angles
    public SerializableVector3 scale;
    public int sortOrder;               // 배치 순서 (Undo용)
    public Dictionary<string, string> customProperties; // 블록별 추가 속성
}

[Serializable]
public class SerializableVector3
{
    public float x, y, z;
}
```

**JSON 예시:**
```json
{
  "mapName": "Stage_01",
  "version": "1.0",
  "gridSize": 2.0,
  "blocks": [
    {
      "blockId": "platform_001",
      "position": { "x": 0, "y": 0, "z": 0 },
      "rotation": { "x": 0, "y": 0, "z": 0 },
      "scale": { "x": 1, "y": 1, "z": 1 }
    },
    {
      "blockId": "obstacle_5_001",
      "position": { "x": 2, "y": 0, "z": 0 },
      "rotation": { "x": 0, "y": 90, "z": 0 },
      "scale": { "x": 1, "y": 1, "z": 1 }
    }
  ]
}
```

### 1-4. MapBlock.cs — 배치된 블록 컴포넌트

씬에 배치된 각 블록 오브젝트에 부착.

```csharp
public class MapBlock : MonoBehaviour
{
    public string blockId;
    public BlockDefinition definition;  // 참조
    public int placementOrder;

    // 그리드 좌표 (스냅된 위치)
    public Vector3Int gridPosition;

    // Bounds 계산 (충돌 감지용)
    public Bounds GetWorldBounds();
}
```

### 1-5. MapRoot.cs — 맵 루트

하나의 맵을 대표하는 루트 오브젝트.

```csharp
public class MapRoot : MonoBehaviour
{
    public string mapName;
    public float gridSize = 2f;
    public List<MapBlock> placedBlocks;

    // 전체 블록 관리
    public void RegisterBlock(MapBlock block);
    public void UnregisterBlock(MapBlock block);
    public MapData ExportToMapData();
    public void ImportFromMapData(MapData data, BlockDatabase db);
}
```

---

## 2단계: 그리드 스냅 시스템 (Editor)

### 2-1. GridSnapSystem.cs

```csharp
public class GridSnapSystem
{
    public float gridUnit = 2f;         // 기본 그리드 단위 (미터)
    public bool snapEnabled = true;
    public bool showGrid = true;
    public Color gridColor = new Color(1, 1, 1, 0.15f);
    public int gridExtent = 50;         // 그리드 표시 범위

    // 핵심 기능
    public Vector3 SnapPosition(Vector3 worldPos);
    public float SnapRotation(float angle, float snapAngle = 15f);
    public Vector3 SnapScale(Vector3 scale, float snapUnit = 0.25f);

    // 그리드 시각화 (Scene View)
    public void DrawGrid(Vector3 center);
    public void DrawBlockGhost(BlockDefinition def, Vector3 snappedPos, Quaternion rot);
}
```

**스냅 로직 상세:**

```csharp
// 위치 스냅: 월드 좌표를 가장 가까운 그리드 포인트로 보정
public Vector3 SnapPosition(Vector3 worldPos)
{
    return new Vector3(
        Mathf.Round(worldPos.x / gridUnit) * gridUnit,
        Mathf.Round(worldPos.y / gridUnit) * gridUnit,
        Mathf.Round(worldPos.z / gridUnit) * gridUnit
    );
}

// snapOffset 적용: 블록마다 다른 원점 보정
public Vector3 SnapWithOffset(Vector3 worldPos, BlockDefinition def)
{
    Vector3 snapped = SnapPosition(worldPos - def.snapOffset);
    return snapped + def.snapOffset;
}
```

**그리드 시각화:** Scene View에 Handles API로 반투명 그리드 라인을 그림. Y축 높이별로 다른 색상 레이어 표시.

---

## 3단계: 에디터 윈도우 (Editor)

### 3-1. MapEditorWindow.cs — 메인 윈도우

`EditorWindow`를 상속한 도킹 가능한 에디터 윈도우.

**UI 레이아웃:**
```
┌──────────────────────────────────────────┐
│  Map Editor                    [Settings]│
├──────────┬───────────────────────────────┤
│ Category │  블록 팔레트 (썸네일 그리드)  │
│ ───────  │  ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ Platform │  │ P1│ │ P2│ │ P3│ │ P4│    │
│ Obstacle │  └───┘ └───┘ └───┘ └───┘    │
│ Wall     │  ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ Tube     │  │ P5│ │ P6│ │ P7│ │ P8│    │
│ Interact │  └───┘ └───┘ └───┘ └───┘    │
│ Aircraft │                               │
│ Props    │  [검색: ____________]          │
├──────────┴───────────────────────────────┤
│ Grid: [2.0]  Snap: [✓]  Rot: [15°]     │
│ Layer: [Y=0 ▼]                           │
├──────────────────────────────────────────┤
│ Map: Stage_01  Blocks: 47               │
│ [Save JSON] [Load JSON] [Clear All]     │
└──────────────────────────────────────────┘
```

**주요 기능:**
- 카테고리 사이드바: 7개 카테고리 탭
- 블록 팔레트: 썸네일 그리드로 표시, 클릭하면 "배치 모드" 진입
- 검색 바: 이름/태그로 필터링
- 그리드 설정: 그리드 크기, 스냅 on/off, 회전 스냅 각도
- 레이어(Y축): 현재 작업 높이 설정
- 맵 관리: 저장, 불러오기, 전체 삭제

### 3-2. BlockPalettePanel.cs

- 프리팹 자동 썸네일 생성 (`AssetPreview.GetAssetPreview`)
- 드래그 앤 드롭 지원 (윈도우에서 Scene View로)
- 선택 시 Scene View에 고스트 프리뷰 표시
- 마우스 오버 시 블록 이름/정보 툴팁

### 3-3. BlockPlacementTool.cs — EditorTool 배치 도구

`EditorTool`을 상속한 커스텀 씬 도구. 블록을 선택한 상태에서 Scene View에서 마우스로 배치.

**배치 워크플로우:**
1. 팔레트에서 블록 클릭 → 배치 모드 활성화
2. Scene View에서 마우스 이동 → 그리드에 스냅된 위치에 반투명 고스트 표시
3. 좌클릭 → 해당 위치에 블록 배치 (Undo 지원)
4. R 키 → 배치 전 90도 회전
5. 마우스 휠 → Y축 높이 조절
6. ESC → 배치 모드 해제

```csharp
[EditorTool("Map Block Placer")]
public class BlockPlacementTool : EditorTool
{
    private BlockDefinition selectedBlock;
    private Vector3 ghostPosition;
    private Quaternion ghostRotation;
    private GameObject ghostPreview;     // 반투명 프리뷰 오브젝트

    public override void OnToolGUI(EditorWindow window)
    {
        // 마우스 레이캐스트 → 스냅 위치 계산
        // 고스트 프리뷰 업데이트
        // 클릭 시 프리팹 인스턴스 생성
        // 키보드 단축키 처리 (R=회전, 휠=높이)
    }
}
```

**고스트 프리뷰:** 배치 전 프리팹의 반투명 복제본을 표시. 쉐이더를 반투명으로 변경하여 "여기에 놓일 것이다"를 시각적으로 보여줌. 그리드에 스냅된 위치에서만 표시되므로 항상 정렬된 결과를 보장.

### 3-4. MapEditorSceneGUI.cs — Scene View 오버레이

Scene View에 추가 정보를 그리는 시스템.

- 3D 그리드 라인 렌더링
- 현재 작업 레이어 하이라이트
- 배치된 블록의 바운딩 박스 표시
- 블록 간 연결/인접 가이드라인
- 스냅 포인트 시각화 (배치 모드 시)

---

## 4단계: 커스텀 인스펙터 (Editor)

### 4-1. BlockInspectorEditor.cs

`MapBlock` 컴포넌트를 위한 커스텀 인스펙터.

**기능:**
- 위치/회전/스케일을 그리드 단위로 표시 & 편집
- 스냅 버튼: 현재 위치를 가장 가까운 그리드로 보정
- 회전 프리셋 버튼: 0°, 90°, 180°, 270° 원클릭 설정
- 스케일 프리셋: 0.5x, 1x, 1.5x, 2x
- 블록 교체: 드롭다운으로 같은 카테고리 내 다른 블록으로 변경
- 복제 버튼: 선택 방향으로 복제 배치

```
┌─ MapBlock Inspector ─────────────────┐
│ Block: platform_001 [▼ Change]       │
│                                       │
│ Grid Position: X[0] Y[0] Z[3]       │
│ [Snap to Grid]                       │
│                                       │
│ Rotation Y: [0°] [90°] [180°] [270°]│
│                                       │
│ Scale: [1.0]  [0.5x][1x][1.5x][2x]  │
│                                       │
│ [Duplicate →] [Duplicate ↑] [Delete] │
└───────────────────────────────────────┘
```

---

## 5단계: 저장/불러오기 (JSON)

### 5-1. MapSerializer.cs

```csharp
public static class MapSerializer
{
    // 저장
    public static void SaveToJson(MapRoot map, string filePath)
    {
        MapData data = map.ExportToMapData();
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(filePath, json);
    }

    // 불러오기
    public static void LoadFromJson(string filePath, MapRoot map, BlockDatabase db)
    {
        string json = File.ReadAllText(filePath);
        MapData data = JsonUtility.FromJson<MapData>(json);
        map.ImportFromMapData(data, db);
    }
}
```

**저장/불러오기 워크플로우:**
- Save: MapRoot 하위 모든 MapBlock을 순회 → PlacedBlockData로 변환 → JSON 직렬화
- Load: JSON 파싱 → 각 PlacedBlockData에 대해 BlockDatabase에서 프리팹 찾기 → Instantiate → MapBlock 부착 → Transform 설정
- 에디터 파일 대화상자 (`EditorUtility.SaveFilePanel / OpenFilePanel`) 사용

---

## 6단계: 편의 기능

### 6-1. 자동 블록 DB 생성 유틸리티

```csharp
[MenuItem("MapEditor/Auto Generate Block Database")]
public static void GenerateBlockDatabase()
{
    // Prefabs 폴더 스캔
    // 폴더명 → BlockCategory 매핑
    // 각 .prefab마다 BlockDefinition 생성
    // 썸네일 자동 캡처
    // BlockDatabase에 등록
}
```

### 6-2. 단축키 목록

| 키 | 기능 |
|----|------|
| G | 그리드 표시 토글 |
| S | 스냅 on/off 토글 |
| R | 선택 블록 90° 회전 |
| Shift+R | 선택 블록 -90° 회전 |
| Delete | 선택 블록 삭제 |
| Ctrl+D | 선택 블록 복제 |
| 마우스 휠 | 배치 높이(Y) 조절 |
| Ctrl+S | 맵 JSON 저장 |
| Ctrl+Z/Y | Undo/Redo (Unity 기본) |
| ESC | 배치 모드 해제 |
| 1~7 | 카테고리 빠른 전환 |

### 6-3. Undo/Redo 지원

모든 블록 배치/삭제/이동은 `Undo.RegisterCreatedObjectUndo`, `Undo.RecordObject` 등 Unity의 Undo 시스템을 사용하여 완전한 되돌리기를 지원.

### 6-4. 복제 배치 (Multi-Place)

Shift+드래그로 일정 방향으로 블록을 연속 배치. 예: 플랫폼을 한 줄로 쭉 깔기.

### 6-5. 기존 스크립트 연동

배치된 블록에 기존 에셋 스크립트를 쉽게 추가할 수 있는 인스펙터 버튼:
- "Add Oscillate Position" → OscillatePosition 컴포넌트 추가
- "Add Rotation" → RotationScript 추가
- "Add Blend Shape Anim" → BlendShapeAnimator 추가

---

## 구현 우선순위

| 순서 | 항목 | 설명 | 예상 난이도 |
|------|------|------|-------------|
| 1 | BlockDefinition + BlockDatabase | 데이터 기반. 자동 생성 포함 | ★★☆ |
| 2 | MapData + MapSerializer | JSON 저장/불러오기 | ★★☆ |
| 3 | MapBlock + MapRoot | 씬 내 블록 관리 컴포넌트 | ★☆☆ |
| 4 | GridSnapSystem | 그리드 스냅 핵심 로직 | ★★☆ |
| 5 | MapEditorWindow (기본) | 에디터 윈도우 + 카테고리 + 팔레트 | ★★★ |
| 6 | BlockPlacementTool | EditorTool 배치 + 고스트 프리뷰 | ★★★ |
| 7 | MapEditorSceneGUI | 그리드 시각화, 가이드라인 | ★★☆ |
| 8 | BlockInspectorEditor | 커스텀 인스펙터 | ★★☆ |
| 9 | 편의 기능 | 단축키, 복제배치, 스크립트 연동 | ★★☆ |

총 C# 스크립트 약 12~15개, 예상 코드량 약 2,500~3,500줄.

---

## 폴더 구조 (유니티 프로젝트 내)

```
Assets/
├── Platformer_2_Obstacles/          # 기존 에셋 (수정 안 함)
│   ├── Prefabs/
│   ├── Meshes/
│   ├── Scripts/
│   └── ...
│
└── MapEditorSystem/                 # 새로 만드는 시스템
    ├── Editor/
    │   ├── MapEditorWindow.cs
    │   ├── BlockPalettePanel.cs
    │   ├── MapEditorSceneGUI.cs
    │   ├── BlockPlacementTool.cs
    │   ├── GridSnapSystem.cs
    │   ├── BlockInspectorEditor.cs
    │   ├── MapEditorSettings.cs
    │   └── BlockDatabaseGenerator.cs
    ├── Runtime/
    │   ├── Data/
    │   │   ├── MapData.cs
    │   │   ├── BlockDefinition.cs
    │   │   └── BlockDatabase.cs
    │   ├── Components/
    │   │   ├── MapBlock.cs
    │   │   └── MapRoot.cs
    │   └── Serialization/
    │       └── MapSerializer.cs
    └── Resources/
        └── BlockDatabase.asset
```
