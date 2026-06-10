# PerfectSync - 시스템 흐름 및 아키텍처

> 최종 갱신 : 2026-03-25
> 대상 브랜치 : `feature/MultiPlay`

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [폴더 구조 및 레이어 규칙](#2-폴더-구조-및-레이어-규칙)
3. [전체 게임 흐름](#3-전체-게임-흐름)
4. [Core 시스템](#4-core-시스템)
5. [Session 시스템](#5-session-시스템)
6. [Race 시스템](#6-race-시스템)
7. [의존성 맵](#7-의존성-맵)
8. [Photon 커스텀 프로퍼티 일람](#8-photon-커스텀-프로퍼티-일람)
9. [주요 설정값](#9-주요-설정값)

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **장르** | 멀티플레이어 팀 레이싱 (래그돌 물리) |
| **네트워크** | Photon PUN 2 |
| **최대 인원** | 8명 (4팀 x 2명) |
| **핵심 루프** | 로비 → 파티/매칭 → 인게임(레이스) → 시상 → 로비 |

---

## 2. 폴더 구조 및 레이어 규칙

```
Assets/02. Scripts/
├── Core/                         # 씬에 독립적인 인프라 계층
│   ├── Network/                  #   Photon 접속·방·파티 관리
│   ├── Team/                     #   팀 배정·조회
│   ├── Shared/                   #   싱글턴, 씬로더, 유틸
│   └── Utilities/                #   카메라 입력 변환, 직렬화 등
│
└── InGame/                       # 인게임 전용 로직
    ├── Session/                  #   게임 상태 머신 · 스폰
    ├── Race/                     #   코스 진행 · 순위 · 완주 처리
    ├── Player/                   #   캐릭터 · 래그돌 · 능력
    ├── Camera/                   #   시네머신 카메라
    ├── UserInput/                #   입력 수집·전송
    ├── Team/                     #   인게임 팀 연출
    ├── Platform/                 #   발판·지형
    └── Obstacle/                 #   장애물
```

모든 하위 시스템은 **3-계층 아키텍처**를 따릅니다:

```
01.Repository   →  Photon 프로퍼티/네트워크 데이터 읽기 (순수 데이터 접근)
02.Domain       →  값 객체 · 열거형 · 비즈니스 규칙 (순수 로직, 외부 의존 없음)
03.Manager      →  싱글턴 매니저, 흐름 제어, 이벤트 발행 (오케스트레이션)
04.UI           →  프레젠테이션 (있는 경우)
```

---

## 3. 전체 게임 흐름

```
┌─────────────────────────────────────────────────────────────────────┐
│                         전체 게임 라이프사이클                         │
└─────────────────────────────────────────────────────────────────────┘

 [로비]                [매칭]               [인게임]              [시상]
   │                     │                    │                    │
   ▼                     ▼                    ▼                    ▼
┌────────┐  파티  ┌────────────┐  입장  ┌──────────┐  전환  ┌──────────┐
│ Photon │──────→│  랜덤 방   │──────→│  레이스   │──────→│  Awards  │
│ 접속   │  매칭  │  생성/참가  │  팀배정 │  진행     │  완료  │  씬      │
└────────┘       └────────────┘       └──────────┘       └────┬─────┘
     ▲                                                        │
     └────────────── 프로퍼티 초기화 · 로비 복귀 ──────────────┘


 ── 상세 단계 ──

 1. PhotonServerManager.Connect()
          │
          ▼
 2. PhotonPartyManager.CreateParty()     ← 파티 생성 (P-XXXXXX)
          │  상대방 JoinParty(code)
          ▼
 3. 양쪽 SetReady(true)
          │  AreAllReady() == true
          ▼
 4. PhotonPartyManager.StartMatchmaking()  ← 파티장만 실행
          │
          ▼
 5. PhotonRoomManager ── 랜덤 방 생성 (R-XXXXXXXX, 최대 8명)
          │  파티원 자동 입장
          ▼
 6. 방 가득 참 감지
          │
          ▼
 7. PhotonTeamManager.AssignTeamsRandomly()  ← 마스터 클라이언트만
          │
          ▼
 8. InGameManager ── Loading → Intro(2s) → Countdown(3s) → Playing
          │
          ▼
 9. 레이스 진행 → 완주/타임아웃 → Awards 씬 전환
```

---

## 4. Core 시스템

### 4-1. Network (`Core/Network/`)

```
┌─────────────────────────────────────────────────────────────┐
│                    Network 계층 구조                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌───────────────────┐                                      │
│  │ PhotonServerManager│  ← Singleton, DontDestroyOnLoad     │
│  │  - Connect()       │     SendRate=40, SerializationRate=30│
│  │  - OnConnected     │                                      │
│  └────────┬──────────┘                                      │
│           │                                                  │
│  ┌────────▼──────────┐     ┌─────────────────────┐          │~~~~
│  │ PhotonRoomManager │────→│ PhotonRoomTypes     │          │
│  │  - JoinRandomRoom  │     │  "rt": random/custom │          │
│  │  - CreateRoom      │     │        /party        │          │
│  │  - MaxPlayers=8    │     └─────────────────────┘          │
│  └────────┬──────────┘                                      │
│           │                                                  │
│  ┌────────▼──────────┐     ┌─────────────────────┐          │
│  │ PhotonPartyManager│────→│ PhotonTeamManager   │          │
│  │  - CreateParty     │     │  (PartyId 설정)      │          │
│  │  - StartMatchmaking│     └─────────────────────┘          │
│  │  - MaxParty=2      │                                      │
│  └───────────────────┘                                      │
│                                                             │
│  ┌───────────────────────┐     ┌───────────────┐            │
│  │PhotonRoomSnapshotReader│───→│ RoomSnapshot   │            │
│  │  - TryGetCurrent()     │     │  (readonly struct)│          │
│  └───────────────────────┘     │  Name, Kind,    │            │
│                                 │  PlayerCount    │            │
│                                 └───────┬───────┘            │
│                                         │                    │
│                                 ┌───────▼───────┐            │
│                                 │   RoomKind     │            │
│                                 │  Unknown=0     │            │
│                                 │  Random=1      │            │
│                                 │  Custom=2      │            │
│                                 │  Party=3       │            │
│                                 └───────────────┘            │
└─────────────────────────────────────────────────────────────┘
```

**접속 → 매칭 시퀀스:**

```
  Client          ServerMgr      PartyMgr       RoomMgr        TeamMgr
    │                │               │              │              │
    │── Connect() ──→│               │              │              │
    │←─ OnConnected ─│               │              │              │
    │                │               │              │              │
    │── CreateParty() ─────────────→│              │              │
    │←─ OnPartyCreated ────────────│              │              │
    │                │               │              │              │
    │── SetReady(true) ───────────→│              │              │
    │   (AreAllReady == true)       │              │              │
    │                │               │              │              │
    │── StartMatchmaking() ────────→│              │              │
    │                │               │──CreateRoom──→│              │
    │                │               │  (R-XXXXXXXX) │              │
    │←─ OnMatchmakingStarted ──────│              │              │
    │                │               │              │              │
    │                │               │   방 가득 참  │              │
    │                │               │              │──AssignTeams──→│
    │                │               │              │  Randomly()   │
    │←─────────────────────────── OnAllTeamsAssigned ──────────────│
    │                │               │              │              │
```

### 4-2. Team (`Core/Team/`)

```
┌───────────────────────────────────────────────────────────┐
│                     Team 계층 구조                          │
├───────────────────────────────────────────────────────────┤
│                                                           │
│  03.Manager                                               │
│  ┌──────────────────────────┐                             │
│  │    PhotonTeamManager     │  ← Singleton, DontDestroy   │
│  │  - SetTeam(int)          │                             │
│  │  - LeaveTeam()           │                             │
│  │  - AssignTeamsRandomly() │  ← 마스터 전용              │
│  │  - GetTeamMembers(int)   │                             │
│  │  - IsTeamFull(int)       │                             │
│  │  - AreAllTeamsAssigned() │                             │
│  │                          │                             │
│  │  Events:                 │                             │
│  │  - OnPlayerTeamChanged   │                             │
│  │  - OnAllTeamsAssigned    │                             │
│  └──────────┬───────────────┘                             │
│             │ 의존                                         │
│  01.Repository                                            │
│  ┌──────────▼───────────────┐   ┌──────────────────────┐  │
│  │PhotonTeamStateRepository │──→│PhotonTeamPropertyKeys│  │
│  │  - GetTeamId(Player)     │   │  Team = "team"       │  │
│  │  - GetTeamRaw(Player)    │   │  PartyId = "pid"     │  │
│  └──────────┬───────────────┘   └──────────────────────┘  │
│             │                                              │
│  02.Domain  │                                              │
│  ┌──────────▼──────┐   ┌────────────────────┐             │
│  │     TeamId      │   │    TeamRules       │             │
│  │ (readonly struct)│   │  MaxTeams = 4      │             │
│  │  NoneRaw = 0    │   │  PlayersPerTeam = 2│             │
│  │  FromPhotonRaw()│   │  IsValidTeam()     │             │
│  │  IsNone         │   └────────────────────┘             │
│  └─────────────────┘                                      │
└───────────────────────────────────────────────────────────┘


  팀 배정 알고리즘 (AssignTeamsRandomly - 마스터 전용):

  플레이어 8명 (파티A 2명, 파티B 2명, 솔로 4명)
       │
       ▼
  1. PartyId 기준 그룹핑
     [파티A: P1,P2] [파티B: P3,P4] [솔로: P5] [솔로: P6] [솔로: P7] [솔로: P8]
       │
       ▼
  2. Fisher-Yates 셔플
       │
       ▼
  3. 4팀에 순차 배정 (팀당 최대 2명, 파티는 같은 팀)
     Team 1: [P3, P4]  ← 파티B (같은 팀 보장)
     Team 2: [P1, P2]  ← 파티A (같은 팀 보장)
     Team 3: [P5, P8]  ← 솔로
     Team 4: [P6, P7]  ← 솔로
```

### 4-3. Shared / Utilities

| 클래스 | 위치 | 역할 |
|--------|------|------|
| `SingletonMonoBehaviour<T>` | Shared | 제네릭 싱글턴 (DontDestroyOnLoad 옵션) |
| `SingletonPunCallbacks<T>` | Shared | 싱글턴 + `MonoBehaviourPunCallbacks` |
| `SceneLoader` | Shared | 로컬/Photon 씬 전환 |
| `Billboard` | Shared | 항상 카메라를 바라보는 UI |
| `CoroutineWaitCache` | Shared | `WaitForSeconds` GC 방지 캐시 |
| `ListExtensions` | Shared | `Shuffle<T>()` 확장 메서드 |
| `CameraRelativeConverter` | Utilities | 카메라 기준 입력 방향 변환 |
| `DualInputCombiner` | Utilities | 두 입력 벡터 합산 (분리 모드용, clamp 1.5) |
| `SerializableDictionary<K,V>` | Utilities | Inspector 직렬화 가능 딕셔너리 |

---

## 5. Session 시스템

### 게임 상태 머신 (GameState)

```
                    모든 플레이어 Ready
                    + 마스터 RPC_StartIntro
  ┌─────────┐  ─────────────────────────→  ┌─────────┐
  │ Loading │                              │  Intro  │
  │         │  방 닫기, 스폰, ReadyKey 설정  │  (2초)  │
  └─────────┘                              └────┬────┘
                                                │
                                                ▼
                                           ┌─────────┐
                                           │Countdown│
                                           │ 3→2→1   │
                                           └────┬────┘
                                                │
                                                ▼
                              ┌─────────────────────────────────┐
                              │           Playing               │
                              │  IsLocalPlayerControllable=true  │
                              └───────┬─────────────┬───────────┘
                                      │             │
                         로컬 팀 완주  │             │  피니시 윈도우
                                      ▼             ▼  타임아웃
                              ┌────────────┐  ┌──────────┐
                              │RaceComplete│  │ GameOver │
                              │FinalRank   │  │FinalRank │
                              │저장         │  │저장       │
                              └─────┬──────┘  └─────┬────┘
                                    │               │
                                    └───────┬───────┘
                                            ▼
                                    Awards 씬 전환
```

### InGameManager 핵심 흐름

```
  InGameManager              PlayerSpawner           PhotonTeamManager
       │                          │                        │
       │ [Awake]                  │                        │
       │  방 닫기                  │                        │
       │  프로퍼티 초기화           │                        │
       │                          │                        │
       │ [Start 코루틴]            │                        │
       │  대기: InRoom &&          │                        │
       │        Team != None      │                        │
       │                          │                        │
       │  팀 호스트 여부 확인       │                        │
       │── SpawnTeamCharacter() ─→│                        │
       │   (호스트만 호출)          │                        │
       │                          │  스폰포인트/폴백 결정    │
       │                          │  PhotonNetwork.Instantiate()
       │                          │                        │
       │                          │  컴포넌트 설정:         │
       │                          │   RaceProgressTracker   │
       │                          │                        │
       │                          │                        │
       │  ReadyKey = true         │                        │
       │                          │                        │
       │ [OnPlayerPropertiesUpdate]│                        │
       │  모든 플레이어 Ready?     │                        │
       │  [마스터만]               │                        │
       │                          │                        │
       │══ RPC_StartIntro ═══════════════════════ 브로드캐스트
       │                          │                        │
       │  GameFlowRoutine:        │                        │
       │  Intro(2s) → Countdown(3s) → Playing              │
       │                          │                        │
```

---

## 6. Race 시스템

### 스플라인 기반 코스 구조

```
  ┌──────┐  Segment A  ┌──────┐  Segment B  ┌──────┐  Segment C  ┌──────┐
  │ CP 0 │────────────→│ CP 1 │────────────→│ CP 2 │────────────→│ CP 3 │
  │START │  (Spline)   │      │  (Spline)   │      │  (Spline)   │FINISH│
  └──────┘             └──────┘             └──────┘             └──────┘

  - 각 RaceSegment = Unity SplineContainer + (FromCheckpoint → ToCheckpoint)
  - RaceCourseTopology: 간선 리스트에서 lastCheckpointIndex 계산
  - RaceRankingManager: 세그먼트 그래프 → 누적 거리 캐시 구축
```

### 순위 계산 흐름

```
  RaceProgressTracker x N (레이서별, 매 프레임 Update)
       │
       │  스플라인에 위치 투영 → 코스 거리 계산
       ▼
  RaceRankingManager (매 프레임 LateUpdate)
       │
       │  팀별 최대 진행도 수집
       ▼
  RaceRankingCalculator.Compute()
       │
       ├─── 체크포인트 >= lastCheckpointIndex?
       │         │                    │
       │        YES                  NO
       │         │                    │
       │         ▼                    ▼
       │    완주팀 처리           미완주팀 처리
       │    finishOrder          진행도 내림차순
       │    순서대로              정렬
       │    1st, 2nd, 3rd...     rank = finishers
       │         │               + progressRank
       │         │                    │
       │         └────────┬───────────┘
       │                  ▼
       │         TeamRankEntry 리스트
       │                  │
       ├──────────────────┼──────────────────────┐
       │                  │                      │
       ▼                  ▼                      ▼
  OnRankingsUpdated  OnFirstPlaceFinished   OnTeamFinished
       │                  │                      │
       ▼                  ▼                      ▼
  TeamRankUI         RaceInfoPresenter      RaceInfoPresenter
  (실시간 순위)      (완주 처리 시작)       (개별 팀 완주 처리)
```

### 완주 처리 상세 흐름

```
  RaceRankingManager       RaceInfoPresenter      Rules              UI            InGameManager
       │                        │                   │                │                  │
       │── OnFirstPlaceFinished─→│                   │                │                  │
       │   (teamNumber)         │                   │                │                  │
       │                        │                   │                │                  │
       │              ┌─────────┴─────────┐         │                │                  │
       │              │                   │         │                │                  │
       │         로컬팀=1등          로컬팀≠1등      │                │                  │
       │              │                   │         │                │                  │
       │              │   OnFirstPlaceFinished()───→│                │                  │
       │              │                   │←────── Decision ────────│                  │
       │              │                   │    (StartFinishWindow)  │                  │
       │              │                   │         │                │                  │
       │              │                   │────── ShowFinishWindow──→│                  │
       │              │                   │         │        (10초)  │                  │
       │              │                   │         │                │                  │
       │              │              ┌────┴────┐    │                │                  │
       │              │              │매초 틱   │    │                │                  │
       │              │              │카운트다운│───→│── ShowSeconds──→│                  │
       │              │              └────┬────┘    │                │                  │
       │              │                   │         │                │                  │
       │              │         ┌─────────┴─────────────┐           │                  │
       │              │         │                       │           │                  │
       │              │    시간 내 완주              10초 타임아웃    │                  │
       │              │         │                       │           │                  │
       │              │         ▼                       ▼           │                  │
       │              │    ShowFinish()           ShowGameOver()    │                  │
       │              │    ("완주")               ("GameOver")      │                  │
       │              │         │                       │           │                  │
       │              ▼         ▼                       ▼           │                  │
       │         ShowWinner()   │                       │           │                  │
       │         ("Winner")     │                       │           │                  │
       │              │         │                       │           │                  │
       │              ▼         ▼                       ▼           │                  │
       │         EnterLocalRaceComplete() ───────────────────────────────────────────→│
       │                                  EnterLocalGameOver() ─────────────────────→│
       │                                                                              │
```

### Awards 전환 (RaceAwardsTransition)

```
  조건 A: 모든 플레이어 RaceDone == true ──┐
                                           ├──→ 3초 딜레이 ──→ PhotonNetwork.LoadScene("Awards")
  조건 B: 1등 완주 후 45초 타임아웃 ────────┘                    (마스터 클라이언트만)
```

---

## 7. 의존성 맵

### 시스템 간 의존 관계

```
  ┌═══════════════════════════════════════════════════════════════════════════┐
  ║  Core (DontDestroyOnLoad)                                               ║
  ║                                                                         ║
  ║   PhotonServerManager ◄── PhotonRoomManager ◄── PhotonPartyManager     ║
  ║                                    │                     │              ║
  ║                                    │   방 가득 시 트리거   │ PartyId 설정 ║
  ║                                    ▼                     ▼              ║
  ║                              PhotonTeamManager ◄─────────┘              ║
  ║                              SceneLoader                                ║
  ║                                                                         ║
  ╠═════════════════════════════════╤════════════════════════════════════════╣
                                    │
       ┌────────────────────────────┼─────────────────────────────┐
       │                            │                             │
       ▼                            ▼                             ▼
  ┌─────────────────┐   ┌──────────────────────┐   ┌─────────────────────┐
  │ Session         │   │ Race                 │   │ (기타 InGame)       │
  │                 │   │                      │   │                     │
  │ InGameManager ──│──→│ RaceInfoPresenter    │   │ Player/             │
  │   │             │   │   │                  │   │ Camera/             │
  │   ▼             │   │   ▼                  │   │ UserInput/          │
  │ PlayerSpawner   │   │ RaceRankingManager   │   │ Platform/           │
  │                 │   │   │                  │   │ Obstacle/           │
  │ InGameRaceKeys ←│───│───┤                  │   │                     │
  │                 │   │   ▼                  │   │                     │
  │ PlayerFinal     │   │ RaceProgressTracker  │   │                     │
  │ RankReader      │   │ RaceRankingCalculator│   │                     │
  │                 │   │ RaceAwardsTransition  │   │                     │
  │ InGameLocal     │   │                      │   │                     │
  │ PlayerProperty  │   │ TeamRankUI           │   │                     │
  │ Reset           │   │ RaceInfoUI           │   │                     │
  └─────────────────┘   └──────────────────────┘   └─────────────────────┘


  ── 상세 의존 화살표 ──

  Session → Core:
    InGameManager        ──→  PhotonTeamManager    (팀 조회)
    InGameManager        ──→  InGameRaceKeys       (프로퍼티 키)
    PlayerSpawner        ──→  PhotonTeamManager    (팀 조회)
    InGameLocalProperty  ──→  PhotonTeamManager    (팀 키)
    InGameLocalProperty  ──→  InGameRaceKeys       (레이스 키)

  Race → Session:
    RaceInfoPresenter    ──→  InGameManager        (상태 전환 요청)
    RaceAwardsTransition ──→  InGameRaceKeys       (키 참조)

  Race → Core:
    RaceProgressTracker  ──→  PhotonTeamManager    (팀 설정)
    RaceAwardsTransition ──→  SceneLoader          (씬 로드)

  Race 내부:
    RaceRankingManager   ──→  RaceRankingCalculator (순위 계산 위임)
    RaceRankingManager   ◄──  RaceProgressTracker   (진행도 수신)
    RaceInfoPresenter    ──→  LocalPlayerRaceFinishRules (규칙)
    RaceInfoPresenter    ──→  RaceInfoUI            (UI 업데이트)
    TeamRankUI           ──→  RaceRankingManager    (순위 구독)
    RaceInfoPresenter    ──→  RaceRankingManager    (이벤트 구독)
```

### 이벤트 구독 관계

```
  ┌─────────── 발행자 ──────────┐          ┌────────── 구독자 ──────────┐
  │                              │          │                            │
  │  PhotonTeamManager           │          │                            │
  │   .OnAllTeamsAssigned  ──────│─────────→│  InGameManager             │
  │   .OnPlayerTeamChanged ──────│─────────→│  (팀 변경 감지)             │
  │                              │          │                            │
  │  InGameManager               │          │                            │
  │   .OnGameStateChanged  ──────│─────────→│  RaceInfoPresenter         │
  │   .OnRaceCountdownTick ──────│─────────→│  RaceInfoPresenter         │
  │                              │          │                            │
  │  RaceRankingManager          │          │                            │
  │   .OnRankingsUpdated   ──────│─────────→│  TeamRankUI                │
  │   .OnFirstPlaceFinished ─────│─────────→│  RaceInfoPresenter         │
  │   .OnTeamFinished      ──────│─────────→│  RaceInfoPresenter         │
  │                              │          │                            │
  └──────────────────────────────┘          └────────────────────────────┘
```

---

## 8. Photon 커스텀 프로퍼티 일람

### Room Properties

| 키 | 값 타입 | 설정자 | 용도 |
|----|---------|--------|------|
| `"rt"` | `string` | `PhotonRoomManager` | 방 종류: `"random"` / `"custom"` / `"party"` |

### Player Properties

| 키 | 값 타입 | 설정자 | 용도 |
|----|---------|--------|------|
| `"team"` | `int` (0~4) | `PhotonTeamManager` | 팀 번호 (0 = 미배정) |
| `"pid"` | `string` | `PhotonPartyManager` | 파티 ID (P-XXXXXX) |
| `"ready"` | `bool` | `PhotonPartyManager` | 파티 내 Ready 상태 |
| `"inGameReady"` | `bool` | `InGameManager` | 인게임 스폰 완료 Ready |
| `"raceDone"` | `bool` | `InGameManager` | 레이스 완료 여부 |
| `"finalRank"` | `int` | `InGameManager` | 최종 팀 순위 (1~4) |

---

## 9. 주요 설정값

| 카테고리 | 항목 | 기본값 | 위치 |
|----------|------|--------|------|
| **네트워크** | SendRate | 40 pkt/s | `PhotonServerManager` |
| | SerializationRate | 30 pkt/s | `PhotonServerManager` |
| | GameVersion | `"0.0.1"` | `PhotonServerManager` |
| **매칭** | 최대 플레이어 | 8명 | `PhotonRoomManager` |
| | 파티 최대 인원 | 2명 | `PhotonPartyManager` |
| | 파티 코드 형식 | `P-XXXXXX` | `PhotonPartyManager` |
| | 게임방 코드 형식 | `R-XXXXXXXX` | `PhotonPartyManager` |
| **팀** | 팀 수 | 4 | `TeamRules.MaxTeams` |
| | 팀당 인원 | 2 | `TeamRules.PlayersPerTeam` |
| **인게임** | 인트로 시간 | 2초 | `InGameManager._introDuration` |
| | 카운트다운 | 3초 | `InGameManager._countdownSeconds` |
| **레이스** | 피니시 윈도우 | 10초 | `RaceInfoPresenter` |
| | Awards 전환 딜레이 | 3초 | `RaceAwardsTransition` |
| | Awards 타임아웃 | 45초 | `RaceAwardsTransition` |
| **입력** | 듀얼 입력 클램프 | 1.5 | `DualInputCombiner` |
