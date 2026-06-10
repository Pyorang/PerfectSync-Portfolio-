# InGame 게임플레이 시스템 아키텍처

> **프레임워크:** Unity + Photon PUN2
> **게임 장르:** 2인 팀 기반 협동 레이싱 (최대 8인 / 4팀)
> **핵심 컨셉:** 합체/분리 모드를 가진 듀얼 아바타 시스템

---

## 1. 전체 시스템 흐름

```
┌──────────────────────────────────────────────────────────────────────┐
│                          프레임 루프                                  │
├────────┬─────────────────────────────────────────────────────────────┤
│ 실행순서 │ 시스템                                                     │
├────────┼─────────────────────────────────────────────────────────────┤
│  -10   │ BodyStateCoordinator    모드 변경 시 권한·동기화 재배선      │
│   -5   │ LocalPlayerInput        키보드/게임패드 입력 읽기             │
│    0   │ InputRouter             입력 라우팅 + RPC 송수신             │
│    0   │ RagdollStateMachine     래그돌 상태 전이 (Authority)         │
│   +1   │ RagdollBoneReceiver     Remote 래그돌 본 보간 적용           │
│   +2   │ PlayerMovement          수평 가속·감속, 접지 판정             │
│    0   │ PlayerJump              점프·다이브 물리 임펄스               │
│   +5   │ FollowCameraController  Cinemachine 프록시 갱신              │
│  +10   │ BodySimulationToggle    Remote 바디 kinematic 보장           │
│ +100   │ BodyMovementSynchronizer 위치·회전·애니메이션 동기화          │
│ +101   │ RagdollBoneSynchronizer  래그돌 본 스냅샷 송수신             │
│ +102   │ RagdollStateNetworkBridge 래그돌 상태 전이 RPC 브로드캐스트  │
├────────┴─────────────────────────────────────────────────────────────┤
│ LateUpdate:                                                          │
│  • PlayerAnimation.ClearGetUpState()                                 │
│  • RagdollBlender.BlendToAnimation() (Authority + Remote 공통)       │
│  • RagdollBoneReceiver → 본 보간 (Remote)                            │
│  • FollowCameraController → 래그돌 카메라 전환 판정                    │
│  • BodyMovementSynchronizer → PhotonTransformView 활성/비활성 제어   │
│  • BodySimulationToggle → kinematic 재보장                            │
└──────────────────────────────────────────────────────────────────────┘
```

### 핵심 흐름 요약

```
[입력] → InputRouter → PlayerFormController.ApplyInput()
                              │
                  ┌───────────┴───────────┐
                  │ Merged                │ Separated
                  │ DualInputCombiner     │ 각 아바타 독립 입력
                  │  → _mergedBody        │  → _avatarA / _avatarB
                  └───────────┬───────────┘
                              ▼
                    IControllableBody.ApplyInput()
                              │
                    PlayerMovement → FixedUpdate → Rigidbody 속도 적용
                    PlayerJump → 점프/다이브 임펄스
                              │
                    [충돌 발생] → HitDetector → OnHitDetected
                              │
                    BodyStateCoordinator.HandleHit()
                              │
                    RagdollStateMachine.ApplyHit()
                              │
               ┌──────────────┼──────────────┐
               │ < stumble     │ ≥ ragdoll    │ BlendToAnim 중
               │ 비틀거림 애니  │ 풀 래그돌     │ 재충격 판정
               └──────────────┴──────────────┘
```

---

## 2. 네트워크 모델 (합체모드 / 분리모드)

### 2.1 공통 구조

| 개념 | 설명 |
|------|------|
| **네트워크 프레임워크** | Photon PUN2 (MonoBehaviourPun, PunRPC, IPunObservable) |
| **권한 모델** | Host-authoritative. Host(PhotonView.IsMine)가 물리 시뮬레이션 최종 권한 |
| **입력 전송** | 양방향 RPC (Host↔Guest), 30Hz 쓰로틀링 (33ms 최소 간격) |
| **입력 변환** | 로컬에서 카메라 상대 방향 → 월드 방향 변환 후 RPC로 전송 |

### 2.2 합체모드 (Merged)

```
┌─────────────────────────────────────────────────────────────┐
│                    합체모드 (Merged)                          │
│                                                              │
│  Guest                          Host                         │
│  ┌──────────┐    RPC 입력     ┌──────────────────────┐      │
│  │ Local    │  ───────────►  │ InputRouter           │      │
│  │ Input    │                │  ├ DualInputCombiner   │      │
│  └──────────┘                │  └→ _mergedBody 제어   │      │
│                              └──────────┬───────────┘      │
│                                         │                    │
│                               물리 시뮬레이션 (Dynamic)       │
│                                         │                    │
│                              BodyMovementSynchronizer        │
│                              (IPunObservable)                 │
│                                         │                    │
│  ┌──────────────────────┐    위치/회전   │                    │
│  │ Guest _mergedBody    │◄──────────────┘                    │
│  │ (Kinematic)          │    + 애니메이션 파라미터 직접 적용   │
│  │ 속도 기반 예측 보간   │    + Jump/Dive/Land RPC            │
│  └──────────────────────┘                                    │
│                                                              │
│  _avatarA, _avatarB: 비활성 (SetActive=false)                │
└─────────────────────────────────────────────────────────────┘
```

**권한 매핑 (합체모드):**

| 바디 | Host | Guest |
|------|------|-------|
| `_mergedBody` | Authority (Dynamic, 물리 시뮬) | Remote (Kinematic, 보간 수신) |
| `_avatarA` | Remote (비활성) | Remote (비활성) |
| `_avatarB` | Remote (비활성) | Remote (비활성) |

**동기화 채널:**
- `BodyMovementSynchronizer` (IPunObservable): position, rotation, speed, grounded, velocity
- 애니메이션 RPC: `RpcAnimJump`, `RpcAnimDive`, `RpcAnimLand`
- Guest에서는 `_rootBody.isKinematic = true`로 수신 위치를 예측 보간으로 수신
  - 예측 위치 = `pos + velocity × lag` (`lag = PhotonNetwork.Time - info.SentServerTime`, 최대 0.2s)
  - `MovePosition`/`Lerp`으로 예측 위치를 향해 보간 (PredictiveInterpolationFactor = 0.3)

### 2.3 분리모드 (Separated)

```
┌─────────────────────────────────────────────────────────────┐
│                    분리모드 (Separated)                       │
│                                                              │
│  Host                              Guest                     │
│  ┌────────────────┐               ┌────────────────┐        │
│  │ InputRouter    │               │ InputRouter     │        │
│  │  → _avatarA    │               │  → _avatarB     │        │
│  │  (Authority)   │               │  (Authority)    │        │
│  │  Dynamic 물리  │               │  Dynamic 물리   │        │
│  └───────┬────────┘               └───────┬────────┘        │
│          │                                │                  │
│   RagdollBoneSynchronizer          RagdollBoneSynchronizer   │
│   RagdollStateNetworkBridge        RagdollStateNetworkBridge │
│          │                                │                  │
│  ┌───────▼────────┐               ┌───────▼────────┐        │
│  │ Guest 쪽       │               │ Host 쪽         │        │
│  │ _avatarA 미러  │               │ _avatarB 미러   │        │
│  │ (Kinematic)    │               │ (Kinematic)     │        │
│  └────────────────┘               └────────────────┘        │
│                                                              │
│  _mergedBody: 비활성 (SetActive=false)                       │
│  AvatarB 소유권: Host → Guest로 TransferOwnership            │
└─────────────────────────────────────────────────────────────┘
```

**권한 매핑 (분리모드):**

| 바디 | Host | Guest |
|------|------|-------|
| `_mergedBody` | Remote (비활성) | Remote (비활성) |
| `_avatarA` | Authority (Dynamic) | Remote (Kinematic) |
| `_avatarB` | Remote (Kinematic) | Authority (Dynamic) |

**소유권 이전:**
- 분리 진입 시: Host가 `_avatarB`의 `PhotonView.TransferOwnership()`으로 Guest에게 소유권 이전
- 합체 복귀 시: Host가 `_avatarB` 소유권을 다시 Host로 회수

### 2.4 모드 전환 흐름

```
[요청] TeamModeSynchronizer.RequestSwitch()
         │
         ├─ Host가 요청 → RpcRequestSwitch (All)
         │
         └─ Guest가 요청 → RpcRelaySwitch (Host만)
                              └→ CanChangeForm() 가드 통과 시
                                  → RpcRequestSwitch (All)
         │
         ▼
InputRouter.HandleSwitchRequested()
         │
         ▼
PlayerFormController.ExecuteFormToggle()
         │
         ├─ 래그돌 활성 중이면 ForceRecover() 선행
         │
         ├─ SwitchToMerged()
         │    두 아바타 평균 위치/속도/방향 → _mergedBody 배치
         │    _avatarA, _avatarB → SetActive(false)
         │
         └─ SwitchToSeparated()
              _mergedBody 위치 기준 좌/우 _separationOffset만큼 분리 배치
              _mergedBody → SetActive(false)
         │
         ▼
OnModeChanged 이벤트 → BodyStateCoordinator.RefreshBodyMode()
                        │
                        ├─ BodySimulationToggle.SetRemote() — kinematic 토글
                        ├─ BodyMovementSynchronizer.SetSyncEnabled() — 위치 동기화
                        ├─ RagdollBoneSynchronizer — 권한 + 활성 설정
                        ├─ RagdollStateNetworkBridge — 권한 설정
                        ├─ RagdollStateMachine — 권한 설정
                        ├─ HitDetector — 권한 설정
                        └─ PhotonView.TransferOwnership() — 분리 시 AvatarB → Guest
```

**전환 가드:** `CanChangeForm()` — 래그돌 활성 상태에서는 전환 불가 (Guest → Host 릴레이 시에도 가드)

---

## 3. 시스템 아키텍처 및 의존성 흐름

```
                        ┌─────────────────────────┐
                        │    TeamModeSynchronizer  │  모드 전환 RPC 릴레이
                        │  (Guest→Host→All)        │
                        └────────────┬────────────┘
                                     │ OnSwitchRequested
┌──────────────┐                     ▼
│ LocalPlayer  │──────►  ┌───────────────────────┐
│ Input        │         │      InputRouter       │
│ (키보드/패드) │  입력   │  (-5 실행순서)         │
└──────────────┘  읽기   │  카메라 상대→월드 변환  │
                         │  RPC 송수신 (30Hz)     │
┌──────────────┐         │  RouteInput()          │
│ RemotePlayer │◄────────│                        │
│ Input        │  RPC    └────────────┬───────────┘
│ (상대방 입력) │                      │ ApplyInput()
└──────────────┘                      ▼
                         ┌───────────────────────┐
                         │  PlayerFormController  │
                         │  모드 상태 관리         │
                         │  합체/분리 전환 실행    │
                         └──┬──────────┬────┬────┘
                            │          │    │
              ┌─────────────┘          │    └──────────────┐
              ▼                        ▼                   ▼
    ┌─────────────────┐    ┌─────────────────┐   ┌─────────────────┐
    │   _mergedBody   │    │    _avatarA     │   │    _avatarB     │
    │ (합체 아바타)    │    │ (Host 아바타)   │   │ (Guest 아바타)  │
    └────────┬────────┘    └────────┬────────┘   └────────┬────────┘
             │                      │                      │
             └──────────┬───────────┘──────────────────────┘
                        │  각 바디 공통 컴포넌트
                        ▼
              ┌──────────────────────────────────────────────────┐
              │              Per-Body 컴포넌트 스택               │
              │                                                  │
              │  Movement ─── PlayerMovement (IControllableBody) │
              │               PlayerJump (점프/다이브)            │
              │               PlayerAnimation (Animator 제어)    │
              │                                                  │
              │  Ragdoll ──── RagdollStateMachine (상태 머신)     │
              │               RagdollRig (IRagdollRig)           │
              │               RagdollBoneReceiver (Remote 수신)  │
              │                                                  │
              │  Network ──── BodyMovementSynchronizer            │
              │               BodySimulationToggle               │
              │               RagdollBoneSynchronizer             │
              │               RagdollStateNetworkBridge           │
              │                                                  │
              │  Collision ── HitDetector (Authority 충돌 감지)   │
              │                                                  │
              │  Camera ───── FollowCameraController              │
              │  Rendering ── SkinnedMeshOffscreenEnabler         │
              └──────────────────────────────────────────────────┘
                        │
                        ▼
    ┌──────────────────────────────────────────────────┐
    │           BodyStateCoordinator (-10)              │
    │           모드 변경 시 전체 재배선                  │
    │                                                   │
    │  • SetRemote() → BodySimulationToggle             │
    │  • SetSyncEnabled() → BodyMovementSynchronizer    │
    │  • SetAuthority() → RagdollBoneSynchronizer       │
    │                      RagdollStateNetworkBridge     │
    │                      RagdollStateMachine           │
    │                      HitDetector                   │
    │  • TransferOwnership() → PhotonView               │
    │  • WireHitDetector() → HitDetector ↔ LocalInput   │
    └──────────────────────────────────────────────────┘
```

### 외부 시스템과의 연결

```
┌────────────────────────────────────┐
│ Obstacle 시스템                     │
│  ObstacleHit (IHitSource)          │──── 충돌 시 HitDetector가 조회
│  ObstacleHitProfile (SO 설정)      │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Platform 시스템                     │
│  PlatformCarrier                   │──── 이동 플랫폼 위 Rigidbody 운반
│  (OnCollisionEnter/Exit 기반)       │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Camera 시스템                       │
│  FollowCameraController            │──── Cinemachine 프록시 + 래그돌 카메라 전환
│  CinemachineTpsInput               │──── Cinemachine 궤도 카메라 입력 제어
│  TpsCameraController               │──── 수동 3인칭 카메라 (Cinemachine 미사용)
│  (Animated/Ragdoll 듀얼 카메라)     │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Session 시스템                      │
│  InGameManager                     │──── 게임 상태 머신 (Loading→Playing→GameOver)
│  PlayerSpawner                     │──── 팀별 스폰 포인트 관리
│  GameState 열거형                   │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Race 시스템                         │
│  RaceRankingManager (Singleton)    │──── 스플라인 기반 실시간 순위 산정
│  RaceProgressTracker               │──── 개별 바디 진행도 추적
│  RaceCheckpoint                    │──── 체크포인트 통과 감지
│  RaceAwardsTransition              │──── 전체 완주 시 시상 씬 전환
└────────────────────────────────────┘
```

---

## 4. 아바타 프리팹 구조

3개의 바디(_mergedBody, _avatarA, _avatarB)가 동일한 프리팹 구조를 공유한다.

```
TeamRoot (PhotonView + InputRouter + PlayerFormController
          + BodyStateCoordinator + TeamModeSynchronizer
          + LocalPlayerInput + RemotePlayerInput + FollowCameraController)
│
├── _mergedBody (GameObject) ─────────────── 합체 아바타
├── _avatarA (GameObject) ────────────────── Host 개별 아바타
└── _avatarB (GameObject) ────────────────── Guest 개별 아바타
     │
     │  각 바디 내부 구조 (3개 모두 동일):
     │
     ├── [Root] ── Rigidbody (_rootBody)
     │             CapsuleCollider
     │             PhotonView
     │             PlayerMovement (IControllableBody)
     │             PlayerJump
     │             PlayerAnimation
     │             RagdollStateMachine
     │             RagdollBoneReceiver
     │             BodyMovementSynchronizer (IPunObservable)
     │             BodySimulationToggle
     │             RagdollBoneSynchronizer (IPunObservable)
     │             RagdollStateNetworkBridge
     │             HitDetector
     │             SkinnedMeshOffscreenEnabler
     │             Transform: _groundCheckPoint
     │
     └── SkeletonRoot (_skeletonRoot) ── Animator
              │
              ├── RagdollRig (IRagdollRig)
              │     스켈레톤 본에 직접 부착된 Rigidbody + Collider 관리
              │
              ├── Hips/Pelvis (_pelvis)
              │     ├── Rigidbody (kinematic 토글)
              │     ├── CapsuleCollider (enable 토글)
              │     │
              │     ├── Spine
              │     │     ├── Rigidbody + CapsuleCollider
              │     │     └── Chest
              │     │           ├── Rigidbody + CapsuleCollider
              │     │           ├── LeftArm → Rigidbody + SphereCollider
              │     │           ├── RightArm → Rigidbody + SphereCollider
              │     │           └── Head → Rigidbody + SphereCollider
              │     │
              │     ├── LeftLeg → Rigidbody + CapsuleCollider
              │     └── RightLeg → Rigidbody + CapsuleCollider
              │
              └── SkinnedMeshRenderer(s)
```

### 핵심 설계: 단일 계층 래그돌

- **비주얼 스켈레톤 본 = 래그돌 본.** 별도의 래그돌 오브젝트가 없다.
- 같은 본의 `Rigidbody.isKinematic`과 `Collider.enabled`을 토글하여 애니메이션↔물리 전환.
- **스켈레톤 분리/재결합:** 래그돌 진입 시 `_skeletonRoot`를 `_rootBody` 자식에서 `transform.root`로 분리 → `rootBody` 이동이 본 물리에 영향을 미치지 않음. 복귀 시 원래 부모로 재결합.

---

## 5. 순수 도메인 클래스

MonoBehaviour를 상속하지 않는 plain C# 클래스/구조체들.

### HitData (readonly struct)

```
경로: Player/HitData.cs
네임스페이스: InGame.Player

필드:
  Knockback   : Vector3   넉백 방향 + 크기
  HitPoint    : Vector3   충돌 지점 (토크 거리 감쇠용)
  Torque      : Vector3   회전력
  Magnitude   : float     knockback.magnitude (임계값 비교용)

유틸리티:
  ComputeRandomTorque(magnitude, scale=0.15) → Random.insideUnitSphere 기반 토크
```

### HitThresholdProfile (ScriptableObject)

```
경로: Player/HitThresholdProfile.cs
네임스페이스: InGame.Player
에셋 메뉴: InGame/Hit Threshold Profile

필드:
  StumbleThreshold : float  기본값 5.0  이 이상이면 비틀거림(stumble) 반응
  RagdollThreshold : float  기본값 7.0  이 이상이면 래그돌(ragdoll) 반응

용도: RagdollStateMachine이 참조하여 충격 임계값 결정
```

### RagdollBoneSnapshot (readonly struct)

```
경로: Player/Ragdoll/RagdollBoneSnapshot.cs
네임스페이스: InGame.Player.Ragdoll

필드:
  BonePositions  : Vector3[]
  BoneRotations  : Quaternion[]

팩토리:
  Capture(IRagdollRig) → 현재 본 위치/회전 스냅샷 생성
```

### RagdollBoneSnapshotSerializer (static class)

```
경로: Player/Network/RagdollBoneSnapshotSerializer.cs
네임스페이스: InGame.Player.Network

메서드:
  Write(snapshot, PhotonStream)  → 본 개수 + position/rotation 직렬화
  Read(PhotonStream) → RagdollBoneSnapshot   역직렬화 (최대 64본 가드)
```

### RagdollBlender (plain class)

```
경로: Player/Ragdoll/RagdollBlender.cs
네임스페이스: InGame.Player.Ragdoll
소유자: RagdollStateMachine이 Start()에서 생성

내부 구조:
  BonePose { Transform, StoredPosition, StoredRotation }

주요 메서드:
  SnapshotRagdollPoses()      래그돌 포즈 캡처 (래그돌 비활성화 전)
  SnapshotRootAlignment()     hip/head/feet 위치 저장 (루트 매칭용)
  StartBlend()                블렌드 타이머 시작
  IsBlendComplete()           블렌드 완료 여부 (MecanimTransitionTime + blendTime)
  BlendToAnimation()          LateUpdate에서 호출. 래그돌 포즈↔애니메이션 포즈 Lerp/Slerp
  AlignRootBodyToPelvis()     루트 바디를 pelvis 위치 + 지면 높이로 정렬

블렌드 타임라인:
  [0ms ~ 50ms]  MecanimTransitionTime — 본을 래그돌 포즈로 고정
  [50ms ~ 550ms] 래그돌 블렌드 — ragdollBlend 1.0→0.0 선형 감소
```

### RagdollHitApplier (plain class)

```
경로: Player/Ragdoll/RagdollHitApplier.cs
네임스페이스: InGame.Player.Ragdoll
소유자: RagdollStateMachine이 Start()에서 생성

주요 메서드:
  ApplyInitialHit(hit, inheritedVelocity)  래그돌 진입 시 — 전체 본 속도 상속 + 거리 기반 분산 충격
  ApplyAdditionalHit(hit)                  래그돌 중 추가 충격 — 기존 속도 유지, 힘만 추가

충격 분산 알고리즘:
  각 본에 대해 HitPoint와의 거리 기반 falloff = 1 - (dist / hitRadius)
  falloff > 0 인 본에 Knockback × falloff × forceScale 적용 (Impulse)
  범위 내 본이 없으면 가장 가까운 본에 전량 적용
```

### RagdollRootTransition (plain class)

```
경로: Player/Ragdoll/RagdollRootTransition.cs
네임스페이스: InGame.Player.Ragdoll
소유자: RagdollStateMachine이 Start()에서 생성

역할: BlendToAnim 진입 시 Remote의 rootBody를 Authority가 보낸 목표 위치/회전으로 부드럽게 전환

주요 메서드:
  Begin(rootBody, targetPos, targetRot)  전환 시작
  Tick(rootBody)                         FixedUpdate에서 SmoothStep 보간
  Cancel()                               전환 취소

보간: t² × (3 - 2t) SmoothStep 커브, 기본 duration = 0.4초
```

### Race 도메인 클래스

```
경로: Race/02.Domain/

RaceTeamProgressInfo (readonly struct):
  TeamNumber           : int
  MaxCheckpointPassed  : int
  BestProgress         : float
  HasFinishedRace      : bool

TeamRankEntry (struct, Serializable):
  TeamNumber   : int
  BestProgress : float
  Rank         : int

RaceFinishEvent (struct):
  TeamNumber  : int
  FinishPlace : int

LocalRaceFinishDecision (struct):
  StopFinishWindowCoroutine    : bool
  StartFinishWindowCoroutine   : bool
  ShowWinner / ShowFinish / ShowGameOver : bool
  DisplayFinishWindowSeconds   : int
  RequestRaceComplete / RequestGameOver : bool

RaceCourseTopology (static class):
  ComputeLastCheckpointIndex(edges) → 코스 그래프에서 마지막 체크포인트 인덱스 산출

RaceRankingCalculator (static class):
  Compute(teamProgress, teamMaxCheckpoint, lastCheckpointIndex,
          finishOrder, newFinishes, rankings, notFinishedBuffer)
  → 완주 팀 순위 확정 + 미완주 팀 진행도 기반 실시간 순위 계산
```

### Session 도메인 클래스

```
경로: Session/02.Domain/

GameState (enum):
  Loading → Intro → Countdown → Playing → RaceComplete → GameOver

InGameLocalPlayerPropertyReset (static class):
  ApplyForLobbyScene(clearTeam) → Ready/RaceDone/FinalRank 커스텀 프로퍼티 초기화

InGameRaceKeys (static class):
  ReadyKey    = "inGameReady"
  RaceDoneKey = "raceDone"
  FinalRankKey = "finalRank"

PlayerFinalRankReader (static class):
  TryGetFinalRank(Player, out int rank) → 플레이어 커스텀 프로퍼티에서 최종 순위 읽기
```

---

## 6. 래그돌 서브시스템

### 6.1 상태 머신

```
                              ┌──────────────────────────────────┐
                              │         RagdollStateMachine       │
                              │      ERagdollState 상태 열거      │
                              └──────────────────────────────────┘

    ┌──────────┐  hit ≥ thresholdProfile.RagdollThreshold  ┌──────────────┐
    │ Animated │ ─────────────────────────────────────────► │  Ragdolled   │
    │          │                                            │              │
    │ 일반 이동 │  StumbleThreshold ≤ hit < RagdollThreshold │ 물리 시뮬     │
    │ 점프/다이브│ ───► Stumble (애니 재생만)                  │ 스켈레톤 분리  │
    │          │      instability 축적                       │ 본 동적 물리  │
    └──────────┘                                            └──────┬───────┘
         ▲                                                         │
         │                                            settled + 접지 체크
         │                                            OR maxDuration(3s) 초과
         │                                                         │
         │          ┌──────────────────┐                           │
         │          │   BlendToAnim    │◄──────────────────────────┘
         │          │                  │
         │          │ 래그돌→애니 블렌드 │
         │          │ GetUp 애니메이션  │
         │          └─────────┬────────┘
         │                    │
         │   OnGetUpComplete() OR IsBlendComplete()
         └────────────────────┘

                        Dead ── 별도 진입점 (EnterDead)
                                래그돌 물리 활성, 복귀 없음
```

### 6.2 임계값 및 파라미터

| 파라미터 | 기본값 | 설명 |
|---------|--------|------|
| `HitThresholdProfile._stumbleThreshold` | 5.0 | 비틀거림 진입 최소 충격 (SO로 에셋별 조정 가능) |
| `HitThresholdProfile._ragdollThreshold` | 7.0 | 풀 래그돌 진입 최소 충격 (SO로 에셋별 조정 가능) |
| `_reImpactMultiplier` | 0.6 | BlendToAnim 중 재래그돌 배수 (7 × 0.6 = 4.2) |
| `_minRagdollDuration` | 0.3s | 최소 래그돌 유지 시간 |
| `_maxRagdollDuration` | 3.0s | 강제 복귀 시간 |
| `_settleVelocity` | 0.5 | 안정 판정 속도 임계값 |
| `_hitRadius` | 2.0 | 충격 분산 반경 |
| `_hitForceScale` | 0.3 | 충격력 스케일 |
| `_ragdollToAnimBlendTime` | 0.5s | 래그돌→애니메이션 블렌드 시간 |
| `_rootTransitionDuration` | 0.4s | 루트 전환 보간 시간 |
| `_instabilityDecayRate` | 4.0/s | 불안정도 초당 감소율 |

### 6.3 Authority 측 전체 흐름

```
1. [충돌]  HitDetector.OnCollisionEnter()
              │
              ▼
2. [판정]  RagdollStateMachine.ApplyHit(HitData)
              │
              ├── effectiveMagnitude = hit.Magnitude + _instability
              │
              ├── < stumbleThreshold → 무시
              ├── ≥ stumbleThreshold & < ragdollThreshold → EnterStumble()
              │     instability += magnitude, 비틀거림 애니 재생
              │
              └── ≥ ragdollThreshold → EnterRagdolled()
                    │
3. [래그돌 진입]    │
    ├── DetachSkeleton() — 스켈레톤을 rootBody에서 분리
    ├── RagdollRig.ActivatePhysics(inheritedVelocity) — 전체 본 Dynamic + Collider 활성
    ├── RagdollHitApplier.ApplyInitialHit() — 거리 기반 충격 분산
    ├── Animator 비활성, rootBody → Kinematic
    ├── pelvis 추적 시작 (FixedUpdate에서 rootBody → pelvis 위치 Lerp)
    └── OnStateChanged → RagdollStateNetworkBridge → RpcEnterRagdolled (Others)
              │
4. [물리 시뮬]  Update에서 settled + 접지 체크
              │
              ▼
5. [복귀 시작]  BeginBlendToAnim()
    ├── RagdollBlender.SnapshotRagdollPoses() — 현재 래그돌 포즈 캡처
    ├── RagdollBlender.SnapshotRootAlignment() — hip/head/feet 위치 저장
    ├── RagdollRig.DeactivateRagdoll() — 전체 본 Kinematic + Collider 비활성
    ├── ReattachSkeleton() — 스켈레톤 재결합
    ├── AlignRootBodyToPelvis() — rootBody를 pelvis 지면 위치로 정렬
    ├── Animator 활성, GetUp 애니메이션 (isFaceUp 판별)
    ├── RagdollBlender.StartBlend()
    └── OnStateChanged → RPC RpcEnterBlendToAnim(pos, rot, isFaceUp)
              │
6. [블렌드]  LateUpdate에서 BlendToAnimation()
    ├── [0~50ms]   Mecanim 전환 대기, 본을 래그돌 포즈로 고정
    └── [50ms~]    ragdollBlend 1.0→0.0 으로 래그돌↔애니메이션 포즈 보간
              │
7. [완료]  OnGetUpComplete() 또는 IsBlendComplete()
    ├── rootBody → Dynamic
    ├── 속도 초기화
    └── 상태 → Animated
```

### 6.4 Remote 측 전체 흐름

```
1. [RPC 수신]  RpcEnterRagdolled()
    ├── RagdollBoneReceiver.StartReceiving() — 본 Kinematic 모드
    ├── RagdollStateMachine.EnterRagdolledRemote()
    │     DetachSkeleton(), ActivateKinematic(), Animator 비활성
    └── rootBody → Kinematic

2. [본 동기화]  RagdollBoneSynchronizer.OnPhotonSerializeView()
    ├── Authority: RagdollBoneSnapshot.Capture() → Serialize → PhotonStream
    └── Remote: Deserialize → RagdollBoneReceiver.ApplySnapshot()
                                    │
                              LateUpdate에서 이전/현재 스냅샷 보간
                              (적응형 receiveInterval로 부드러운 보간)
                              + rootBody를 pelvis 위치로 이동 (카메라 추적용)

3. [RPC 수신]  RpcEnterBlendToAnim(rootPos, rootRot, isFaceUp)
    ├── RagdollBoneReceiver.StopReceiving()
    ├── RagdollBlender.SnapshotRagdollPoses() — BoneReceiver가 위치시킨 포즈 캡처
    ├── RagdollRig.DeactivateRagdoll()
    ├── ReattachSkeleton()
    ├── RagdollRootTransition.Begin(rootBody, rootPos, rootRot) — 루트 보간 시작
    ├── Animator 활성, GetUp 애니메이션
    └── RagdollBlender.StartBlend()

4. [블렌드]  Authority와 동일하게 LateUpdate에서 BlendToAnimation()
    ├── 단, 루트 매칭은 RagdollRootTransition이 담당 (Authority 전송 위치로 SmoothStep)

5. [RPC 수신]  RpcEnterAnimated()
    ├── BoneReceiver.StopReceiving()
    ├── EnterAnimatedRemote()
    │     ReattachSkeleton(), Animator 활성
    └── rootBody → Dynamic 복원
```

### 6.5 래그돌 서브시스템 컴포넌트 관계도

```
┌─────────────────────────────────────────────────────────────────┐
│                    RagdollStateMachine (상태 머신)                │
│  소유: RagdollBlender, RagdollHitApplier, RagdollRootTransition │
│  참조: RagdollRig, Animator, PlayerAnimation, HitThresholdProfile│
│  이벤트: OnStateChanged, OnStumblePlayed                        │
└─────┬──────────────┬─────────────────┬─────────────────────────┘
      │              │                 │
      ▼              ▼                 ▼
┌───────────┐  ┌───────────┐   ┌──────────────────────┐
│ RagdollRig│  │ Ragdoll   │   │ RagdollBoneReceiver  │
│(IRagdollRig)│ │ Blender   │   │ (Remote 전용)        │
│            │  │ (plain)   │   │                      │
│ 본 물리    │  │ 포즈 블렌드│   │ 네트워크 스냅샷 수신  │
│ 관리      │  │ 루트 정렬  │   │ 본 보간 적용          │
└───────────┘  └───────────┘   └──────────────────────┘
      ▲                               ▲
      │ Capture                        │ ApplySnapshot
      │                                │
┌───────────────────────────────────────────────┐
│         RagdollBoneSynchronizer                │
│  Authority: 스냅샷 캡처 → PhotonStream 쓰기    │
│  Remote: PhotonStream 읽기 → BoneReceiver 전달 │
└───────────────────────────────────────────────┘

┌───────────────────────────────────────────────┐
│         RagdollStateNetworkBridge              │
│  Authority: OnStateChanged → RPC 브로드캐스트  │
│  Remote: RPC 수신 → StateMachine Remote 메서드 │
│          + BoneReceiver Start/Stop 제어        │
└───────────────────────────────────────────────┘
```

---

## 7. 충돌 처리 시스템

### 7.1 아키텍처 개요

```
[물리 충돌]
     │
     ▼
┌─────────────────────────────────────────────────┐
│              HitDetector (Per-Body)               │
│  Authority에서만 동작 (_isAuthority 가드)          │
│                                                   │
│  OnCollisionEnter(Collision)                      │
│  ├── 레이어 체크 (_hazardLayers 비트마스크)        │
│  │                                                │
│  ├── IHitSource 조회 (충돌 대상에서)              │
│  │   ├── 있으면: TryComputeKnockback() 호출       │
│  │   └── 없으면: Fallback (상대 속도 기반)         │
│  │              _minKnockback(3) 미만이면 무시     │
│  │                                                │
│  └── OnHitDetected(HitData) 이벤트 발생           │
└───────────────────────┬─────────────────────────┘
                        │
                        ▼
┌───────────────────────────────────────────────┐
│  BodyStateCoordinator.WireHitDetector()        │
│  HitDetector.OnHitDetected                     │
│    → LocalPlayerInput.SendHit(hit, viewID)     │
│      ├── 로컬: OnHitReceived 즉시 invoke        │
│      └── RPC: RpcReceiveHit → Others           │
│                                                │
│  LocalPlayerInput.OnHitReceived                │
│    → BodyStateCoordinator.HandleHit()          │
│      → ResolveBodyByViewID() 로 바디 식별       │
│      → RagdollStateMachine.ApplyHit()          │
└───────────────────────────────────────────────┘
```

### 7.2 IHitSource 인터페이스

```csharp
// Player/IHitSource.cs
public interface IHitSource
{
    bool TryComputeKnockback(Collision collision, out Vector3 knockback, out Vector3 torque);
}
```

구현체: `ObstacleHit`

### 7.3 ObstacleHit + ObstacleHitProfile

```
┌─────────────────────────────────────────────────────────────────────┐
│  ObstacleHit (MonoBehaviour, IHitSource)                            │
│  장애물 오브젝트에 부착                                               │
│                                                                     │
│  ScriptableObject 참조: ObstacleHitProfile                          │
│  내부 상태: Dictionary<colliderID, lastHitTime> 쿨다운 관리           │
│  자동 정리: 16개 초과 시 만료 엔트리 제거                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│  ObstacleHitProfile (ScriptableObject)                              │
│  [CreateAssetMenu: InGame/Obstacle Hit Profile]                     │
│                                                                     │
│  ┌── 넉백 모드 (EKnockbackMode) ──────────────────────────────┐    │
│  │  VelocityScaled : 상대속도 × _knockbackStrength              │    │
│  │  Fixed          : 고정값 _knockbackStrength                  │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                     │
│  ┌── 넉백 방향 (EKnockbackDirection) ─────────────────────────┐    │
│  │  FromCollision  : 상대속도 방향 (기본)                       │    │
│  │  ContactNormal  : 접촉면 법선                                │    │
│  │  ObstacleForward: 장애물 transform.forward                   │    │
│  │  Custom         : _customDirection 직접 지정                 │    │
│  └──────────────────────────────────────────────────────────────┘    │
│                                                                     │
│  ┌── 추가 옵션 ───────────────────────────────────────────────┐    │
│  │  _upwardBias      : 상향 성분 혼합 비율 (0~1)               │    │
│  │  _alwaysRagdoll   : true면 magnitude ≥ _minRagdollMagnitude │    │
│  │  _torqueScale     : 토크 스케일 (기본 0.15)                  │    │
│  │  _cooldown        : 같은 Collider 재히트 방지 시간           │    │
│  └──────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

### 7.4 Fallback 넉백 (IHitSource 없는 충돌)

```
HitDetector.TryComputeFallbackKnockback():
  knockback = collision.relativeVelocity
  magnitude < _minKnockback(3) → 무시
  torque = HitData.ComputeRandomTorque(magnitude)
```

### 7.5 HitData 전달 흐름 (네트워크)

```
[Authority Host에서 충돌 감지]
         │
         ▼
HitDetector.OnHitDetected(HitData)
         │
         ▼ (BodyStateCoordinator가 연결)
LocalPlayerInput.SendHit(hit, viewID)
         │
         ├── 로컬: OnHitReceived.Invoke(hit, viewID)  ← Host가 즉시 처리
         │
         └── RPC: RpcReceiveHit(knockback, hitPoint, viewID, torque)
                                    │
                                    ▼  (Guest 수신)
                         LocalPlayerInput.RpcReceiveHit()
                                    │
                                    ▼
                         OnHitReceived.Invoke(hit, viewID)
                                    │
                                    ▼
                         BodyStateCoordinator.HandleHit(hit, viewID)
                                    │
                         ResolveBodyByViewID() → 대상 바디 식별
                                    │
                                    ▼
                         RagdollStateMachine.ApplyHit()
                         (Authority가 아니면 무시)
```

### 7.6 PlatformCarrier (이동 플랫폼)

```
경로: Platform/PlatformCarrier.cs
네임스페이스: InGame.Race.Platform
실행 순서: +1

역할: 이동/회전하는 플랫폼 위의 Rigidbody를 함께 운반

동작:
  OnCollisionEnter → Dynamic Rigidbody를 _riders 리스트에 추가
  OnCollisionExit  → 제거
  FixedUpdate      → 플랫폼 이동분(deltaPos, deltaRot) 계산
                      riders에 위치/회전 적용

특징:
  - Trigger 불필요 (기존 Collider 사용)
  - 이동 + 회전 모두 지원
  - Kinematic 바디는 제외 (isKinematic 체크)
```

---

## 8. 세션 시스템 (Session)

게임 라이프사이클을 관리하는 시스템. 씬 로드부터 게임 종료까지의 흐름을 담당한다.

### 8.1 게임 상태 머신

```
┌───────────┐    씬 로드 완료     ┌──────────┐    모든 플레이어 Ready
│  Loading  │ ──── 대기 중 ────► │  Intro   │ ── RPC_StartIntro ──►
│ (초기 상태)│    팀 배정+스폰     │ (연출)    │    _introDuration(2s)
└───────────┘                    └──────────┘
                                                          │
     ┌────────────────────────────────────────────────────┘
     ▼
┌───────────┐    매 초 OnRaceCountdownTick    ┌──────────┐
│ Countdown │ ──── _countdownSeconds(3) ────► │ Playing  │
│ (카운트다운)│                                 │ (플레이) │
└───────────┘                                 └────┬─────┘
                                                   │
                              ┌─────────────────────┤
                              ▼                     ▼
                    ┌──────────────┐       ┌──────────┐
                    │ RaceComplete │       │ GameOver │
                    │ (로컬 완주)   │       │ (제한시간)│
                    └──────────────┘       └──────────┘
```

### 8.2 InGameManager (SingletonPunCallbacks)

```
경로: Session/03.Manager/InGameManager.cs
역할: 마스터 게임 흐름 오케스트레이터

주요 필드:
  _introDuration     : float  기본값 2.0s  인트로 연출 시간
  _countdownSeconds  : int    기본값 3     카운트다운 초

이벤트:
  OnGameStateChanged(GameState)   상태 전환 시 발행
  OnRaceCountdownTick(int)        카운트다운 매 초 발행

정적 프로퍼티:
  IsLocalPlayerControllable → CurrentState == Playing 일 때만 true

라이프사이클:
  Awake()  → 커스텀 프로퍼티 초기화, 방 입장 차단
  Start()  → 팀 배정 대기 → PlayerSpawner.SpawnTeamCharacter() (팀 호스트만)
  OnPlayerPropertiesUpdate() → 전원 Ready 감지 → RPC_StartIntro (MasterClient)
  GameFlowRoutine() → Intro(2s) → Countdown(3s, 매초 tick) → Playing

종료 처리:
  EnterLocalRaceComplete() → 최종 순위 저장 + RaceDone 프로퍼티 설정
  EnterLocalGameOver()     → 동일
```

### 8.3 PlayerSpawner

```
경로: Session/03.Manager/PlayerSpawner.cs
역할: 팀별 스폰 포인트에 TeamCharacter 인스턴스화

스폰 포인트: _teamCharacterSpawnPoints[4] (팀당 1개)
  폴백: _fallbackBasePosition + 팀 인덱스 × _fallbackSpacing

SpawnTeamCharacter(teamNumber):
  1. 팀 번호 유효성 검증
  2. 스폰 포인트 또는 폴백 위치 결정
  3. PhotonNetwork.Instantiate() 호출
  4. RaceProgressTracker에 팀 번호 설정
```

### 8.4 씬 전환 (RaceAwardsTransition)

```
경로: Race/03.Manager/RaceAwardsTransition.cs
역할: MasterClient 전용 — 전체 완주 감지 후 시상 씬 로드

감시: OnPlayerPropertiesUpdate → RaceDoneKey 확인
조건: 모든 현재 접속 플레이어가 RaceDone == true
전환: _delayBeforeLoadSeconds(3s) 후 Awards 씬 로드
안전장치: 첫 RaceDone 감지 후 _maxWaitAfterFirstRaceDoneSeconds(45s) 초과 시 강제 전환
```

---

## 9. 레이스 시스템 (Race)

스플라인 기반 레이스 코스에서 팀별 실시간 순위를 산출하고, 완주/제한시간 로직을 처리한다.

### 9.1 아키텍처 개요

```
┌───────────────────────────────────────────────────────────────────┐
│                    RaceRankingManager (Singleton)                   │
│  코스 토폴로지 분석 + 거리 캐시 + 실시간 순위 산출                    │
│                                                                    │
│  BuildSegmentLookup()                                              │
│    RaceSegment[] → Dictionary<fromCheckpoint, List<RaceSegment>>   │
│                                                                    │
│  RebuildRaceDistanceCaches()                                       │
│    Bellman-Ford 최장경로 알고리즘으로 각 세그먼트의 레이스 내 절대거리   │
│    산출 (분기 코스 지원)                                              │
│                                                                    │
│  CalculateRankings() — LateUpdate에서 매 프레임 호출                 │
│    1. GetAllTeamsProgress() → 팀별 최고 진행도 집계                   │
│    2. RaceRankingCalculator.Compute() → 순위 산출                    │
│    3. 완주 팀 이벤트 발행                                            │
└──────────┬────────────────────────────────┬───────────────────────┘
           │ 등록                            │ 이벤트
           ▼                                ▼
┌─────────────────────┐         ┌──────────────────────────────┐
│ RaceProgressTracker │         │ OnRankingsUpdated(rankings)   │→ TeamRankUI
│ (Per-Body)          │         │ OnFirstPlaceFinished(team)    │→ RaceInfoPresenter
│                     │         │ OnTeamFinished(team, place)   │→ RaceInfoPresenter
│ Update():           │         └──────────────────────────────┘
│  스플라인 최근접점    │
│  → 절대 거리 산출    │
│  → Progress 갱신    │
│                     │
│ PassCheckpoint(idx) │◄── RaceCheckpoint.OnTriggerEnter()
└─────────────────────┘
```

### 9.2 코스 구조

```
[RaceSegment]  하나의 스플라인 경로 구간
  ├── _fromCheckpoint : int  시작 체크포인트 인덱스
  ├── _toCheckpoint   : int  종료 체크포인트 인덱스
  └── SplineContainer       Unity Spline 데이터

[RaceCheckpoint]  트리거 콜라이더 기반 체크포인트
  └── _checkpointIndex : int  체크포인트 고유 인덱스

코스 토폴로지 (그래프):
  체크포인트 = 노드, 세그먼트 = 간선
  분기 코스 지원: 한 체크포인트에서 여러 세그먼트로 갈라질 수 있음
  최장 경로 기준 거리 캐시 (Bellman-Ford relaxation)
```

### 9.3 진행도 추적 (RaceProgressTracker)

```
각 바디(Per-Body)에 부착, 팀 번호 보유

매 프레임 Update():
  1. 현재 위치에서 코스 스플라인 최근접점 탐색
  2. 정규화된 스플라인 t 값 → RaceRankingManager.GetDistanceAlongRace() → 절대 거리
  3. Progress 프로퍼티 갱신

PassCheckpoint(index):
  _checkpointsPassed = max(_checkpointsPassed, index)
  → 뒤로 돌아가도 진행도 유지

RaceRankingManager에 OnEnable/OnDisable로 자동 등록/해제
```

### 9.4 순위 산출 (RaceRankingCalculator)

```
static Compute():
  입력: teamProgress, teamMaxCheckpoint, lastCheckpointIndex, finishOrder
  출력: newFinishes(새 완주 이벤트), rankings(전체 순위)

로직:
  1. 완주 판정: maxCheckpoint >= lastCheckpointIndex 인 팀
  2. 완주 팀: finishOrder 순서대로 순위 부여
  3. 미완주 팀: progress 내림차순 정렬 → 완주팀 뒤에 순위 배정
```

### 9.5 완주 윈도우 로직 (LocalPlayerRaceFinishRules)

```
1팀이 1등 완주 시:
  → 로컬 팀이면: ShowWinner 표시
  → 다른 팀이면: 제한시간 카운트다운 시작 (_finishWindowSeconds = 10)

로컬 팀 완주 시:
  → ShowFinish + 카운트다운 중지

제한시간 만료 시:
  → ShowGameOver + RequestGameOver
  → 미완주 팀은 GameOver 상태로 전환

상태 밀봉: 한번 결과가 확정되면 _outcomeSealed = true → 이후 결정 무시
```

### 9.6 Race UI

```
RaceInfoPresenter (오케스트레이터):
  InGameManager.OnGameStateChanged → 카운트다운/시작 표시
  RaceRankingManager.OnFirstPlaceFinished → 완주 윈도우 처리
  RaceRankingManager.OnTeamFinished → 로컬 팀 완주 처리
  FinishWindowRoutine() → 매초 남은 시간 갱신

RaceInfoUI (뷰):
  ShowCountdown(n)              "3", "2", "1"
  ShowStart()                   "START!"
  ShowWinner()                  "WINNER!"
  ShowFinish()                  "FINISH!"
  ShowGameOver()                "GAME OVER"
  ShowFinishWindowSeconds(n)    남은 시간 표시
  FadeRoutine() → hold(0.85s) → fade(0.65s) 패턴

TeamRankUI (실시간 순위):
  OnRankingsUpdated 구독 → 로컬 팀 순위를 "1st", "2nd" 등으로 표시
```

---

## 10. 플레이어 매니저 시스템 (Player Manager)

### 10.1 Ability 패턴

```
PlayerAbility (abstract MonoBehaviour)
  └── Owner : PlayerController (부모에서 자동 탐색)

PlayerController (MonoBehaviour)
  [RequireComponent: PhotonView, CharacterController, PlayerCursorLockController]
  └── GetAbility<T>() : 타입별 캐시 딕셔너리로 능력 컴포넌트 조회

구현체:
  ├── PlayerMoveAbility   이동 + 점프 + 중력
  │     _moveSpeed=5, _jumpPower=5, _gravity=-9.81
  │     CharacterController.Move() 기반
  │
  └── PlayerRotateAbility  마우스 회전 + Cinemachine 카메라 설정
        _rotationSpeed=200
        SetFollowCamera(CinemachineCamera) → 카메라 추적 대상 설정
```

### 10.2 커서 제어

```
PlayerCursorLockController:
  로컬 플레이어만 활성
  Alt 키로 커서 잠금/해제 토글
  InGameManager.IsLocalPlayerControllable 연동
```

### 10.3 PlayerNameUI

```
경로: Player/04.UI/PlayerNameUI.cs
역할: 플레이어 이름 + 팀 컬러 표시
  Start() → PhotonView 소유자 닉네임 설정 + 팀 비교 후 컬러 적용
```

---

## 11. 카메라 시스템 (Camera)

### 11.1 FollowCameraController (메인 인게임 카메라)

```
경로: Camera/PlayerCamera/FollowCameraController.cs
네임스페이스: InGame.Camera.PlayerCamera
실행 순서: +5
상속: MonoBehaviourPun

역할: 팀 모드에 따라 합체/분리 아바타를 추적하며 래그돌 카메라 자동 전환

구조:
  Animated Camera (CinemachineCamera) — 기본 애니메이션 상태 카메라
  Ragdoll Camera  (런타임 복제)       — 래그돌 상태 전용 (damping 조정)

프록시 시스템:
  _animatedProxyRb — 애니메이션 카메라 추적 대상 (Kinematic Rigidbody)
  _ragdollProxyRb  — 래그돌 카메라 추적 대상 (Kinematic Rigidbody)
  FixedUpdate에서 프록시를 실제 바디 위치로 MovePosition

카메라 전환:
  LateUpdate → RagdollStateMachine.IsPhysicsRagdoll 감시
  래그돌 진입: Ragdoll Camera Priority=10, Animated=0
  래그돌 복귀: Animated Camera Priority=10, Ragdoll=0
  Cinemachine이 자동 블렌딩 처리
```

### 11.2 CinemachineTpsInput (Cinemachine 궤도 입력)

```
경로: Camera/03.Manager/CinemachineTpsInput.cs
역할: CinemachineOrbitalFollow에 마우스 입력 주입

주요 설정:
  horizontalSensitivity=3, verticalSensitivity=2
  orbitRadius=4, targetOffset=(0, 1, 0)
  pitchRange=(-20, 60)

Awake()에서 CinemachineOrbitalFollow 컴포넌트 자동 설정
Update()에서 Mouse Delta → Horizontal/Vertical Axis 갱신
```

### 11.3 TpsCameraController (수동 3인칭 카메라)

```
경로: Camera/03.Manager/TpsCameraController.cs
역할: Cinemachine 없이 직접 카메라 위치/회전 계산

LateUpdate():
  마우스 입력 → yaw/pitch 갱신 (pitchRange 클램프)
  Quaternion.Euler(pitch, yaw, 0) × Vector3.back × distance + target.position
  transform.LookAt(target + targetOffset)

SetTarget(Transform) → 런타임 추적 대상 변경
```

---

## 부록: 실행 순서 상수

```csharp
// Core/ExecutionOrderConstants.cs
public static class ExecutionOrderConstants
{
    // ── 초기화 (-10) ──
    public const int BodyStateCoordinator = -10;

    // ── 입력 (-5) ──
    public const int LocalPlayerInput = -5;

    // ── 래그돌 코어 (0~2) ── 상태 전이 → 본 적용 → 이동 순서 보장
    public const int RagdollStateMachine = 0;
    public const int RagdollBoneReceiver = 1;
    public const int PlayerMovement = 2;

    // ── 카메라 (+5) ──
    public const int CinemachineCameraManager = 5;

    // ── 후처리 (10+) ──
    public const int BodySimulationToggle = 10;
    public const int BodyMovementSynchronizer = 100;
    public const int RagdollBoneSynchronizer = 101;
    public const int RagdollStateNetworkBridge = 102;
}
```

## 부록: 인터페이스 요약

| 인터페이스 | 경로 | 구현체 | 핵심 역할 |
|-----------|------|--------|----------|
| `IControllableBody` | Player/Movement/ | `PlayerMovement` | 입력 적용, 속도 제어, 래그돌 상태 노출 |
| `IHitSource` | Player/ | `ObstacleHit` | 충돌 시 넉백/토크 계산 |
| `IPlayerInput` | UserInput/ | `LocalPlayerInput`, `RemotePlayerInput` | 입력 데이터 + 히트/사망 네트워크 경계 |
| `IRagdollRig` | Player/Ragdoll/ | `RagdollRig` | 본 물리 관리 (Dynamic/Kinematic 전환, 안정 판정) |
| `IPunObservable` | Photon.Pun | `BodyMovementSynchronizer`, `RagdollBoneSynchronizer` | Photon 직렬화 스트림 |

## 부록: 파일 트리

```
Assets/02. Scripts/InGame/
│
├── Camera/
│   ├── 03.Manager/
│   │   ├── CinemachineTpsInput.cs            Cinemachine 궤도 카메라 입력
│   │   └── TpsCameraController.cs            수동 3인칭 카메라 컨트롤러
│   └── PlayerCamera/
│       └── FollowCameraController.cs         Cinemachine 프록시 + 래그돌 카메라 전환
│
├── Obstacle/
│   ├── ObstacleHit.cs                        IHitSource 구현, 쿨다운 관리
│   └── ObstacleHitProfile.cs                 넉백 설정 ScriptableObject
│
├── Platform/
│   └── PlatformCarrier.cs                    이동 플랫폼 라이더 운반
│
├── Player/
│   ├── 03.Manager/
│   │   ├── PlayerAbility.cs                  능력 베이스 클래스
│   │   ├── PlayerController.cs               능력 캐시 + PhotonView 관리
│   │   ├── PlayerCursorLockController.cs     커서 잠금 제어
│   │   ├── PlayerMoveAbility.cs              이동 + 중력 능력
│   │   └── PlayerRotateAbility.cs            회전 + 카메라 설정 능력
│   │
│   ├── 04.UI/
│   │   └── PlayerNameUI.cs                   플레이어 이름 + 팀 컬러
│   │
│   ├── Animation/
│   │   └── PlayerAnimation.cs                Animator 파라미터 래퍼
│   │
│   ├── Movement/
│   │   ├── IControllableBody.cs              이동 인터페이스
│   │   ├── PlayerMovement.cs                 수평 이동, 접지 판정, 회전
│   │   └── PlayerJump.cs                     점프, 다이브
│   │
│   ├── Network/
│   │   ├── BodyMovementSynchronizer.cs       위치/애니메이션 동기화
│   │   ├── BodySimulationToggle.cs           Remote 바디 kinematic 전환
│   │   ├── BodyStateCoordinator.cs           모드 변경 시 전체 권한 재배선
│   │   ├── RagdollBoneSnapshotSerializer.cs  본 스냅샷 직렬화
│   │   ├── RagdollBoneSynchronizer.cs        래그돌 본 네트워크 동기화
│   │   ├── RagdollStateNetworkBridge.cs      래그돌 상태 전이 RPC
│   │   └── TeamModeSynchronizer.cs           모드 전환 RPC 릴레이
│   │
│   ├── Ragdoll/
│   │   ├── IRagdollBoneRig.cs                래그돌 리그 인터페이스
│   │   ├── RagdollBlender.cs                 래그돌→애니메이션 본 블렌드
│   │   ├── RagdollBoneReceiver.cs            Remote 래그돌 본 보간
│   │   ├── RagdollBoneRig.cs                 단일 계층 래그돌 본 관리
│   │   ├── RagdollBoneSnapshot.cs            본 위치/회전 스냅샷 구조체
│   │   ├── RagdollHitApplier.cs              거리 기반 충격 분산
│   │   ├── RagdollRootTransition.cs          루트 바디 SmoothStep 전환
│   │   └── RagdollStateMachine.cs            래그돌 상태 머신
│   │
│   ├── Rendering/
│   │   └── SkinnedMeshOffscreenEnabler.cs    래그돌 시 컬링 방지
│   │
│   ├── Test/  (디버깅/테스트 전용)
│   │   ├── CoopInputVisualizer.cs            합체 입력 화살표 시각화
│   │   ├── HitTestTrigger.cs                 래그돌 히트 테스트
│   │   ├── InputArrowBuilder.cs              화살표 LineRenderer 생성
│   │   ├── ModeSwitchTestManager.cs          모드 전환 테스트
│   │   └── NetworkTestManager.cs             Photon 네트워크 테스트
│   │
│   ├── HitData.cs                            충돌 데이터 구조체
│   ├── HitDetector.cs                        Authority 충돌 감지
│   ├── HitThresholdProfile.cs                충격 임계값 ScriptableObject
│   ├── IHitSource.cs                         넉백 계산 인터페이스
│   └── PlayerFormController.cs               합체/분리 모드 관리
│
├── Race/
│   ├── 02.Domain/
│   │   ├── LocalPlayerRaceFinishRules.cs     완주 윈도우 상태 머신
│   │   ├── LocalRaceFinishDecision.cs        완주 결정 구조체
│   │   ├── RaceCourseTopology.cs             코스 그래프 분석
│   │   ├── RaceFinishEvent.cs                완주 이벤트 구조체
│   │   ├── RaceRankingCalculator.cs          순위 산출 (static)
│   │   ├── RaceTeamProgressInfo.cs           팀 진행 정보 구조체
│   │   └── TeamRankEntry.cs                  순위 엔트리 구조체
│   │
│   ├── 03.Manager/
│   │   ├── RaceAwardsTransition.cs           전체 완주 → 시상 씬 전환
│   │   ├── RaceCheckpoint.cs                 트리거 기반 체크포인트
│   │   ├── RaceInfoPresenter.cs              레이스 UI 오케스트레이터
│   │   ├── RaceProgressTracker.cs            스플라인 기반 진행도 추적
│   │   ├── RaceRankingManager.cs             싱글톤 순위 관리자
│   │   └── RaceSegment.cs                    스플라인 코스 세그먼트
│   │
│   └── 04.UI/
│       ├── RaceInfoUI.cs                     레이스 메시지 표시 (fade 연출)
│       └── TeamRankUI.cs                     실시간 순위 표시
│
├── Session/
│   ├── 01.Repository/
│   │   └── PlayerFinalRankReader.cs          최종 순위 커스텀 프로퍼티 읽기
│   │
│   ├── 02.Domain/
│   │   ├── GameState.cs                      게임 상태 열거형
│   │   └── InGameLocalPlayerPropertyReset.cs 세션 간 프로퍼티 초기화
│   │
│   └── 03.Manager/
│       ├── InGameManager.cs                  게임 상태 머신 (싱글톤)
│       ├── InGameRaceKeys.cs                 커스텀 프로퍼티 키 상수
│       └── PlayerSpawner.cs                  팀별 스폰 관리
│
├── Team/
│   └── ETeamMode.cs                          { Separated, Merged } 열거형
│
└── UserInput/
    ├── IPlayerInput.cs                       입력 + 네트워크 경계 인터페이스
    ├── InputRouter.cs                        입력 라우팅 + RPC (30Hz)
    ├── LocalPlayerInput.cs                   로컬 키보드/패드 입력
    └── RemotePlayerInput.cs                  원격 입력 수신 버퍼
```
