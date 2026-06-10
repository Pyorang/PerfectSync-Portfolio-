# Troubleshooting: 멀티플레이 래그돌 메시 스트레칭 (DefaultExecutionOrder)

## 1. 문제 현상

멀티플레이 테스트 중, **Guest 측 캐릭터가 래그돌 상태에 진입하면 메시가 치즈처럼 늘어나는 현상**이 발생했다.

- Host 측에서는 정상적으로 래그돌이 재생됨
- Guest 측에서만 SkinnedMesh가 극단적으로 스트레칭됨
- **특정 컴퓨터 환경에서만 재현**되고, 다른 컴퓨터에서는 같은 코드(같은 Git 커밋)임에도 정상 동작

---

## 2. 원인 탐색 과정

### 2-1. 재현 조건 특정

- 문제가 발생하기 시작한 정확한 커밋을 식별: `9439809` (ImpactDetector SRP 리팩터링)
- 해당 커밋은 순수 리팩터링 — 로직 변경 없이 책임 분리와 의존 방향 정리만 수행
- 직렬화 코드(`RagdollBoneSnapshotSerializer`)는 기존 코드의 완전한 복사-이동
- **로직이 바뀌지 않았는데 문제가 발생** → 타이밍/실행 순서 문제를 의심

### 2-2. 시스템 구조 분석

프로젝트의 래그돌 시스템은 **Guest 측에서 여러 독립된 시스템이 같은 Transform을 수동으로 조립**하는 구조:

```
RagdollStateMachine   → 스켈레톤 분리/재결합, rootBody 위치 추적
RagdollBoneReceiver   → 네트워크 스냅샷으로 본 위치를 직접 설정
PlayerMovement        → rootBody 속도/위치를 직접 설정
```

이 세 스크립트 모두 `[DefaultExecutionOrder]`가 지정되어 있지 않아 **Unity 기본값 0**으로 실행되고 있었다.

### 2-3. 핵심 발견: Unity의 실행 순서 정책

Unity 공식 문서에 명시된 사항:

> *"The execution order of methods is undefined for MonoBehaviours that share the same order."*

**같은 ExecutionOrder 값의 스크립트들은 상대적 실행 순서가 정의되지 않는다.**
이 순서는 다음 요인에 의해 달라질 수 있다:

- 스크립트 컴파일 순서 (파일 시스템 열거 순서에 의존)
- Unity 프로젝트 임포트 순서 / Library 캐시 상태
- 에디터 메타데이터 (.meta GUID 순서)
- OS 및 파일시스템 차이

**→ 같은 코드라도 컴퓨터마다 order 0 스크립트들의 실행 순서가 달라질 수 있다.**

---

## 3. 근본 원인

### 3-1. 왜 Guest에서만 발생하는가

싱글플레이나 Host 측에서는 Animator 또는 물리 엔진이 본을 **일관되게 구동**한다.
실행 순서가 달라도 다음 프레임에 자동 보정되어 시각적 차이가 거의 없다.

반면 Guest 측에서는:

```
네트워크에서 본 스냅샷 수신 → BoneReceiver가 본 위치를 수동 설정
네트워크에서 상태 RPC 수신  → StateMachine이 스켈레톤 분리/재결합 수행
로컬 로직                  → PlayerMovement가 rootBody를 직접 조작
```

**Animator나 물리 엔진 같은 중재자 없이** 여러 시스템이 같은 Transform을 직접 조작한다.
실행 순서가 곧 최종 렌더링 결과가 되며, 자동 보정 메커니즘이 없다.

### 3-2. 문제가 되는 구체적 시나리오

래그돌 → BlendToAnim 전환 시점 (Guest):

```
RPC 도착 → EnterBlendToAnimRemote() 실행:
  1. BoneReceiver 수신 중단
  2. 현재 래그돌 포즈 캡처
  3. 스켈레톤을 rootBody에 재결합  ← 이 시점부터 rootBody 이동 = 본 이동
  4. RootTransition 시작 (rootBody 보간)
  5. Animator 재활성화
  6. Blender가 LateUpdate에서 포즈 보간
```

이 전환이 완료된 프레임의 FixedUpdate/LateUpdate에서:

| 컴퓨터 A (정상) | 컴퓨터 B (메시 스트레칭) |
|---|---|
| `RagdollStateMachine` 먼저 실행 | `PlayerMovement` 먼저 실행 |
| → rootBody 위치를 전환 목표로 설정 | → rootBody 상태 체크 전에 조작 시도 |
| → PlayerMovement는 래그돌 상태 확인 후 스킵 | → rootBody 위치 불일치 |
| → Blender가 일관된 데이터로 보간 | → Blender가 불일치 데이터로 보간 → **스트레칭** |

### 3-3. 이 프로젝트가 특히 민감한 이유

일반적인 멀티플레이 캐릭터 동기화(위치/회전만 보정)와 달리, 이 프로젝트는:

1. **스켈레톤 분리/재결합**: 래그돌 진입 시 스켈레톤을 rootBody에서 분리하고, 복귀 시 재결합. 한 프레임 안에서 본의 소유권 자체가 전환됨.
2. **래그돌 ↔ 메카님 블렌딩**: BlendToAnim 구간에서 rootBody, 본 Transform, Animator 세 가지를 동시에 조율해야 함.
3. **2인 합체/분리 모드**: 모드 전환 시 3개 바디(MergedBody, AvatarA, AvatarB)의 authority, kinematic, sync 설정이 한꺼번에 재구성됨.

이 세 요소가 겹치면서, 같은 프레임 안에서 **여러 시스템이 동일한 Transform을 조작하는 구간**이 많아지고, 실행 순서에 대한 의존성이 높아진다.

---

## 4. 해결

### 4-1. `ExecutionOrderConstants`에 명시적 순서 부여

```csharp
public static class ExecutionOrderConstants
{
    // 초기화
    public const int BodyStateCoordinator = -10;
    public const int LocalPlayerInput = -5;

    // 래그돌 코어 — 상태 전이 → 본 적용 → 이동 순서 보장
    public const int RagdollStateMachine = 0;
    public const int RagdollBoneReceiver = 1;
    public const int PlayerMovement = 2;

    // 카메라
    public const int CinemachineCameraManager = 5;

    // 후처리
    public const int BodySimulationToggle = 10;
    public const int BodyMovementSynchronizer = 100;
    public const int RagdollBoneSynchronizer = 101;
    public const int RagdollStateNetworkBridge = 102;
}
```

### 4-2. 각 스크립트에 `[DefaultExecutionOrder]` 어트리뷰트 적용

```csharp
[DefaultExecutionOrder(ExecutionOrderConstants.RagdollStateMachine)]   // 0
public class RagdollStateMachine : MonoBehaviour { }

[DefaultExecutionOrder(ExecutionOrderConstants.RagdollBoneReceiver)]   // 1
public class RagdollBoneReceiver : MonoBehaviour { }

[DefaultExecutionOrder(ExecutionOrderConstants.PlayerMovement)]        // 2
public class PlayerMovement : MonoBehaviour, IControllableBody { }
```

### 4-3. 보장되는 실행 순서

```
매 프레임:
  FixedUpdate/Update/LateUpdate 모두에서:
    RagdollStateMachine (0)  → 상태 전이, rootBody 추적/전환 완료
    RagdollBoneReceiver (1)  → 확정된 상태를 기반으로 본 위치 적용
    PlayerMovement      (2)  → 래그돌 상태를 정확히 읽고 가드 처리
```

이로써 어떤 컴퓨터에서든 동일한 실행 순서가 보장되며, Guest 측의 래그돌 메시 스트레칭 문제가 해결된다.

---

## 5. 교훈

1. **멀티플레이에서 같은 Transform을 여러 시스템이 조작하는 경우, `DefaultExecutionOrder`는 선택이 아닌 필수.** 싱글플레이에서는 Animator/물리 엔진이 자동 보정해주지만, 멀티플레이 Guest는 수동 조립이므로 보정 주체가 없다.

2. **"순수 리팩터링"도 실행 순서 문제를 유발할 수 있다.** 스크립트 파일의 생성/이동/분리는 Unity의 내부 컴파일 순서를 변경시킬 수 있으며, 이는 `DefaultExecutionOrder`가 없는 스크립트들의 상대적 실행 순서에 영향을 준다.

3. **컴퓨터 환경에 따라 재현 여부가 달라지는 버그는, 네트워크 문제만이 아니라 Unity 엔진 레벨의 비결정적 동작도 의심해야 한다.**
