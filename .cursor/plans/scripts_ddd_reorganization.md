# 스크립트·폴더 재구성 계획 (SOLID + DDD 계층 + Layered Feature)

> 보완 이력: **(A) 팀 도메인 명시적 도입**, **(B) PlayerSpawner는 InGame**, 기타 폴더 규약.

## 현재 상태 요약

- 총 C# 약 39개 중 **제외 대상**: `TestCharacterController.cs`, `Player/Ragdoll/` 전체.
- `InGame/Race/`에 `01/02/03` 폴더 메타만 있고 스크립트는 루트에 있음.
- `PlayerController` / `InGameManager` / `RaceRankingManager` 등은 다중 책임·점진 분리 대상.
- 장애물 `ObstacleBase`는 아직 없음 — 신규 시 동일 패턴 적용.

## 목표 폴더 규약

- **의존 방향**: `03.Manager` → `02.Domain` → `01.Repository` (역참조 금지).
- **계층 생략**: 외부 I/O가 없으면 Domain+Manager만, 순수 유틸은 `Core/Shared` 등 **계층 없음** 허용.

## Team 도메인 — 1차 범위에 포함 (의도)

팀은 로비·인게임·시상 등 **여러 영역에서 동일한 규칙**으로 쓰이므로, **`Core` 아래 독립 Feature**로 두고 **도메인 모델을 명시적으로 둔다.** Photon은 “저장·전송”이지 팀 규칙의 근원이 아니다.

### 배치 (권장)

`Core/Team/` (Package by Layered Feature의 2단 “기능”)

| 계층 | 역할 | 예시 |
|------|------|------|
| **02.Domain** | 팀 식별·불변 규칙·슬롯/인원 제한 등 **순수 로직** (Unity/Photon 참조 없음) | `TeamId` 값 객체(유효 범위 `1..MaxTeams`, `None` 구분), `TeamRules` 또는 정적 규칙 클래스(인원 상한, 슬롯 인덱스 계산), 필요 시 `TeamRoster` 같은 경량 모델 |
| **01.Repository** | 방/플레이어의 팀 정보 **읽기·쓰기** (Photon `CustomProperties` 등) | `ITeamStateReader` / `ITeamStateWriter` 또는 `PhotonTeamPropertiesRepository` — int ↔ `TeamId` 매핑은 여기 또는 Domain 팩토리에서 |
| **03.Manager** | 씬 수명·Photon 콜백·유스케이스 조합 | 기존 `PhotonTeamManager`를 이쪽으로 두고, 내부에서 Domain·Repository만 사용하도록 점진 리팩터 |

**의존**: `PhotonTeamManager`(Manager) → Domain(`TeamId`, 규칙) → Repository(프로퍼티 접근). Domain은 Repository/Photon을 **참조하지 않음**.

### 기존 코드와의 관계

- `PhotonTeamManager.TeamKey`, `TeamNone`, `MaxTeams`, `PlayersPerTeam` 등 **상수·불변식**은 Domain으로 옮기거나 Domain이 단일 출처가 되게 정리한다.
- UI·레이스·스폰에서 쓰는 “내 팀 번호 int”는 장기적으로 **`TeamId`** 로 통일하고, 경계(Repository/Manager)에서만 원시값과 변환한다.
- 다른 Feature(`InGame`, `OutGame`, `Awards`)는 가능하면 **`TeamId` / 도메인 서비스**만 알고, Photon 타입을 직접 쓰지 않도록 줄인다 (완전 분리는 단계적이어도 됨).

### 다른 Feature의 Domain 폴더와의 구분

- **Team만** 1차에서 도메인 타입을 풍성하게 두고, Race/Session 등은 기존처럼 **필요 시 점진 추출**해도 된다. (팀은 공용 언어이므로 우선 명시.)

## PlayerSpawner 위치 — Core가 애매한 이유

- **사용처**: `InGameManager` 직렬화 참조, `InGame` 씬 오브젝트로만 등장 (grep 기준).
- **역할**: 팀별 스폰 포인트, `PhotonNetwork.Instantiate`, Cinemachine·`PlayerRotateAbility`·`RaceProgressTracker` **인게임 조립** — 네트워크 “공용 인프라”라기보다 **인게임 세션/스폰 유스케이스**에 가깝다.

**권장**: `PlayerSpawner`는 **Core/Network가 아니라 InGame** 으로 옮긴다.

- 예: `InGame/Session/03.Manager` (또는 `InGame/Spawn/03.Manager`) — `InGameManager`와 같은 Feature에 두면 응집도가 좋다.

**대비**: `PhotonTeamManager`는 **Core/Team/03.Manager**에 둔다 (위 “Team 도메인” 절 참고). 로비·인게임·시상 등 **씬을 가리지 않고** 쓰이므로 `Core`에 두되, **Room/Party와는 별 Feature**로 분리한다. “어디에 플레이어를 스폰할지”는 `PlayerSpawner`(InGame) 책임.

## 권장 최상위 트리 (요약)

| 영역 | Feature | 비고 |
|------|---------|------|
| **Core** | **Team** | **02.Domain** (`TeamId`, 팀 규칙), **01.Repository** (커스텀 프로퍼티), **03.Manager** (`PhotonTeamManager`) |
| **Core** | **Network** | 룸/파티/서버 등 — **PlayerSpawner·팀 도메인 제외** |
| **Core** | **Shared** | `SceneLoader`, 싱글톤 베이스, 확장 메서드 등 |
| **InGame** | **Session** | `InGameManager`, `InGameRaceKeys`, **`PlayerSpawner`** |
| **InGame** | **Race** | 레이스 트래킹·순위·관련 UI |
| **InGame** | **Player** | 컨트롤러·어빌리티·이름 UI (제외 스크립트 제외) |
| **InGame** | **Camera** | TPS 카메라 |
| **OutGame** | **Lobby** | 로비·매칭 UI |
| **OutGame** | **Awards** | 시상 씬 |

**제외**: `Player/TestCharacterController.cs`, `Player/Ragdoll/` — 이동·리팩터 제외.

## 마이그레이션 순서 (갱신)

1. 폴더 스켈레톤 생성 (**`Core/Team/{01,02,03}`** 포함).
2. **`Team` 도메인 스켈레톤**: `TeamId`·규칙 클래스 추가 → `PhotonTeamManager` 및 팀 프로퍼티 읽는 코드에서 **점진적으로** 사용 (한 번에 전부 바꾸지 않아도 됨).
3. **PlayerSpawner를 InGame(Session 또는 Spawn)으로 이동** — `InGameManager`·씬 참조 갱신 (스폰은 `TeamId`/Repository 경유로 연결하는 것을 목표로).
4. 나머지 파일 이동 (제외 항목 제외), Unity 에디터에서 `.meta` 유지 권장.
5. 네임스페이스 정리.
6. Race / Session / Player 순으로 Domain 추출·SRP 분리.

## 리스크·주의

- 스폰 경로 변경 시 **InGame 씬**의 컴포넌트 참조 확인.
- `PlayerSpawner`가 `Core/Network`에 있을 때의 이유(Photon Instantiate)는 **InGame에서도 동일**하므로, 네임스페이스만 `InGame`으로 바뀌면 된다.

## TODO (추적용)

- [ ] 폴더 스켈레톤 (Shared는 계층 생략, **Core/Team 포함**)
- [ ] **Team 도메인**: `TeamId` + 규칙(Domain), 프로퍼티 어댑터(Repository), `PhotonTeamManager` 정리(Manager)
- [ ] **PlayerSpawner → InGame** 이동 및 참조 수정
- [ ] 나머지 스크립트 이동 (제외 2곳 제외)
- [ ] 네임스페이스 정리
- [ ] Race Domain 추출 → Session 분리 → Player SOLID
- [ ] (선택) asmdef로 의존 방향 강제
