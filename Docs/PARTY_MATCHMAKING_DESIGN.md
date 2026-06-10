# PerfectSync 파티 및 매칭 시스템 설계서

## 1. 개요

### 1.1 목표

PUN2 기반 멀티플레이어 레이싱 게임 PerfectSync에서, 2인 파티 시스템과 8인 매칭 시스템을 구현한다. 기존 구조(별도 파티 방 생성 → 게임 방 이동)를 폐기하고, "로비 방 = 중앙 큐 서버" 패턴으로 전환한다.

### 1.2 기존 구조의 문제점

기존 `PhotonPartyManager`는 비공개 파티 방을 별도로 생성하고, 매칭 시 파티 리더가 새 게임 방을 `CreateRoom`으로 만드는 구조였다. 이 구조에서는 파티 A 리더와 파티 B 리더가 각각 독립적으로 `CreateRoom`을 호출하기 때문에 서로 다른 방에 갇혀 만날 수 없었다. 또한 방 프로퍼티 기반 `JoinRandomRoom`을 사용할 때, 리더가 입장한 후 멤버가 뒤따라 들어가기 전에 다른 솔로 플레이어가 슬롯을 빼앗는 TOCTOU(Time-of-Check to Time-of-Use) Race Condition이 발생할 수 있었다.

### 1.3 새 구조 핵심 원리

모든 플레이어가 하나의 로비 방에 상주한다. MasterClient가 매칭 큐를 단독으로 처리하여 동시성 문제를 원천 차단한다. 파티는 별도 방 없이 Player Custom Property만으로 관리한다. 매칭 결과는 `RaiseEvent`로 해당 플레이어에게만 전달하여 배치 간 충돌을 방지한다.

---

## 2. 로비 방

### 2.1 방 설정

앱 시작 시 Photon 서버에 접속한 후, `JoinRandomRoom(filter: rt="lobby")`으로 기존 로비 방에 합류한다. 로비 방이 없으면 `OnJoinRandomFailed`에서 아래 설정으로 새 방을 만든다.

```
MaxPlayers       = 0 (제한 없음, 실질 상한 약 500명)
IsVisible        = true
IsOpen           = true
Room Type ("rt") = "lobby"
CustomRoomPropertiesForLobby = ["rt"]
```

`MaxPlayers = 0`은 PUN2에서 "소프트 리밋을 걸지 않는다"는 의미이며, 학교 프로젝트 규모(동접 수십 명)에서는 성능 문제가 없다. 프로퍼티 브로드캐스트 부하는 50~100명 수준에서 안전하다.

### 2.2 로비 방이 꽉 차는 경우

현실적으로 발생하기 어렵다. 매칭 성사 시 8명이 즉시 게임 방으로 빠지므로 로비에는 대기 중인 플레이어만 남는다. 방어적으로, `JoinRandomRoom`이 실패하면 동일한 설정으로 새 로비 방을 생성한다. 이 경우 두 로비의 매칭 큐는 독립적으로 동작하며 크로스 매칭은 불가하다. 학교 프로젝트 규모에서는 허용 가능한 트레이드오프이다.

---

## 3. 파티 시스템

### 3.1 파티 제약

파티는 2인 고정이다. 3인 이상은 지원하지 않는다.

### 3.2 파티 초대 흐름

모든 플레이어가 같은 로비 방에 있으므로, `PhotonNetwork.PlayerList`에서 상대를 직접 찾을 수 있다. 별도 파티 방을 만들 필요가 없다.

1단계: 초대자가 상대의 UserId를 UI에서 복사/붙여넣기로 입력한다.

2단계: `PlayerList`에서 해당 UserId를 가진 플레이어를 검색한다. 찾지 못하면 "플레이어가 로비에 없습니다"를 표시한다. (게임 중이거나 오프라인)

3단계: `RaiseEvent`로 해당 플레이어에게 파티 초대 이벤트를 전송한다.

4단계: 상대 UI에 초대 팝업이 표시된다. 수락/거절을 선택한다.

5단계: 수락 시, 양쪽 모두 Player Custom Property를 세팅한다.

```
"partyId"      = 생성된 GUID 또는 "P-XXXXXX" 형태의 코드
"partyMembers" = [A의 UserId, B의 UserId]
"ready"        = false (파티 구성 직후 레디 초기화)
```

같은 방이므로 상대의 `"costume"` 등 Custom Property를 바로 읽을 수 있어서, 파티 상대의 캐릭터 외형을 UI에 표시하는 것이 간단하다.

### 3.3 파티 해제

PUN2는 "파티"라는 개념을 모르므로, Custom Property를 수동으로 정리해야 한다. 자동으로 정리되지 않는다. 처리해야 할 케이스는 세 가지이다.

케이스 1 — 직접 해제 (UI에서 "파티 나가기" 버튼): 자기 프로퍼티를 초기화하고 (`partyId = null`, `partyMembers = null`, `ready = false`), 상대에게 `RaiseEvent`로 파티 해제 이벤트를 전송한다. 상대는 이벤트를 받으면 자기 프로퍼티도 동일하게 초기화한다.

케이스 2 — 파티 상대 접속 끊김/로비 나감: `OnPlayerLeftRoom`에서 나간 플레이어가 내 파티 멤버인지 확인한다. 맞다면 내 파티 프로퍼티를 초기화한다.

케이스 3 — 게임 종료 후 로비 복귀: 이 경우에는 프로퍼티를 건드리지 않는다. `partyId`가 Player Custom Property에 남아있으므로 파티 상태가 자동으로 유지된다. 로비에 복귀하면 `OnPlayerEnteredRoom`으로 상대를 감지하여 파티 UI를 다시 표시한다.

---

## 4. 매칭 큐 시스템 (MatchQueueManager)

### 4.1 설계 원리

MasterClient에서만 동작하는 `MatchQueueManager`가 매칭 큐를 단독 관리한다. 큐를 처리하는 주체가 하나뿐이므로 동시성 문제가 원천 차단된다. 동시성 비유로 보면 단일 스레드 이벤트 루프(Node.js 모델)와 같다.

### 4.2 큐 진입

플레이어가 레디 버튼을 누르면 `"ready" = true`를 Player Custom Property로 세팅한다. MasterClient는 `OnPlayerPropertiesUpdate`를 받아 해당 플레이어를 큐에 추가한다.

솔로 플레이어는 1슬롯짜리 단독 엔트리로 큐에 들어간다. 파티는 2슬롯짜리 원자적 단위로 큐에 들어간다. 파티에서는 두 명 다 레디해야 큐에 진입한다. 한 명만 레디한 상태에서는 큐에 추가하지 않는다.

### 4.3 큐 처리 알고리즘

MasterClient가 큐 앞에서부터 순서대로 8슬롯을 채운다.

```
slots = 8
batch = []

for entry in queue:
    size = entry.memberCount   // solo = 1, party = 2
    if slots - size >= 0:
        slots -= size
        batch.add(entry)
    else:
        skip (이 엔트리는 큐에 남김)
    if slots == 0:
        break
```

예시: 큐에 [S1, S2, Party(P1+P2), S3, Party(P3+P4), S4, S5, S6]가 있을 때, 앞에서부터 채우면 S1(7), S2(6), P1+P2(4), S3(3), P3+P4가 2슬롯을 요구하므로 남은 3슬롯에 들어가고(1), S4(0). 최종 배치: [S1, S2, P1+P2, S3, P3+P4, S4] — 총 8명. 만약 1슬롯만 남은 상태에서 다음이 2인 파티면 스킵하고 솔로를 넣는다. 8명이 정확히 채워졌을 때만 매칭을 확정한다.

핵심: 파티를 쪼개지 않는다. 남은 슬롯보다 파티 인원이 크면 스킵하고 다음 솔로나 더 작은 파티를 탐색한다.

### 4.4 매칭 결과 전달: RaiseEvent

8슬롯이 채워지면 MasterClient가 게임 방 이름을 생성하고, 매칭된 8명에게만 `RaiseEvent`로 전달한다.

```
EventCode    = MatchConfirmed (커스텀 이벤트 코드)
Content      = { "room": "G-a8f2e1" }
TargetActors = [매칭된 8명의 ActorNumber 배열]
SendOptions  = SendReliable
```

Room Property가 아닌 `RaiseEvent(TargetActors)`를 사용하는 이유: Room Property로 `gameTarget`을 세팅하면, 첫 번째 배치 8명이 나가기 전에 MasterClient가 두 번째 배치를 위해 값을 덮어쓸 수 있다. 아직 안 나간 플레이어가 새 값을 읽어버리는 충돌이 발생한다. `RaiseEvent`는 특정 플레이어에게만 전달되므로 배치 간 간섭이 없다.

MasterClient는 이벤트를 보낸 즉시 해당 8명을 큐에서 제거한다. `OnPlayerLeftRoom`을 기다리지 않는다. 이로써 바로 다음 배치 처리를 시작할 수 있다.

### 4.5 큐 상태 보존 (MasterClient 이탈 대비)

큐 상태를 Room Custom Property `"matchQueue"`에 JSON으로 직렬화하여 저장한다. 큐에 변경이 있을 때마다 갱신한다.

```
"matchQueue" = "[{\"type\":\"solo\",\"userId\":\"abc\"},{\"type\":\"party\",\"userIds\":[\"def\",\"ghi\"]}]"
```

MasterClient가 이탈하면 PUN2가 자동으로 새 MasterClient를 지정한다. 새 MasterClient는 `OnMasterClientSwitched`에서 `"matchQueue"` 프로퍼티를 읽어 큐를 복원하고 처리를 이어간다.

---

## 5. 게임 방 이동

### 5.1 게임 방 입장

매칭 이벤트를 수신한 8명은 다음 순서로 게임 방에 진입한다.

1단계: Player Custom Property에 `"lobbyRoomName"` = 현재 로비 방 이름을 저장한다. (복귀용)

2단계: 로비 방을 나간다. (`LeaveRoom`)

3단계: `OnLeftRoom`에서 `JoinOrCreateRoom(gameRoomName, gameRoomOptions, TypedLobby.Default)`을 호출한다.

`JoinOrCreateRoom`은 제일 먼저 도착한 사람이 방을 생성하고 나머지는 자동으로 합류한다. 누가 `CreateRoom`하고 누가 `JoinRoom`할지 정할 필요가 없다.

### 5.2 게임 방 설정

```
MaxPlayers       = 8
IsVisible        = false (외부인 랜덤 합류 차단)
IsOpen           = true (매칭된 플레이어는 방 이름을 알고 있으므로 합류 가능)
Room Type ("rt") = "game"
```

`IsVisible = false`이므로 `JoinRandomRoom`으로 외부인이 들어올 수 없다. 매칭된 8명만 방 이름을 알고 있어서 `JoinOrCreateRoom`으로만 입장 가능하다.

### 5.3 입장 타임아웃 처리

게임 방 MasterClient(제일 먼저 도착한 사람)가 15초 타이머를 시작한다.

8명 전원 도착 시: 타이머를 취소하고 게임 카운트다운을 시작한다.

15초 내 미달 시 (6~7명 도착): 현재 인원으로 게임을 시작한다. 팀 배정을 유연하게 조정한다. 누락된 사람이 파티원이었으면 남은 파트너를 솔로로 전환하여 팀을 재배정한다.

5명 이하 도착 시: 게임 불가로 판단하고 전원 로비로 복귀한다. 자동 레디로 재큐잉한다.

### 5.4 팀 배정

기존 `PhotonTeamManager.AssignTeamsRandomly()`를 그대로 재사용한다. 이 메서드는 이미 `partyId`로 파티 그룹을 판별하여 같은 팀에 배정하는 로직이 구현되어 있다. 파티 2인은 같은 팀으로 자동 배정되고, 솔로 플레이어는 남은 자리에 랜덤 배정된다.

---

## 6. 게임 종료 및 복귀

### 6.1 정상 종료

레이스가 끝나면 시상식(Awards) 씬을 거친 후, 게임 방을 나간다. `OnLeftRoom`에서 저장해둔 `lobbyRoomName`으로 `JoinRoom`을 호출하여 로비 방에 복귀한다.

복귀 시 `"ready"`를 `false`로 리셋한다. `"partyId"`는 그대로 유지되므로 파티 상태가 보존된다. 로비 UI에서 `OnPlayerEnteredRoom`으로 파티 상대의 복귀를 감지하여 파티 UI를 다시 표시한다.

### 6.2 카운트다운 중 취소

게임 방에서 카운트다운 진행 중 누군가 나가면, 나머지 전원이 로비로 복귀한다. 이때 `returnReason = "cancelled"` 플래그를 들고 복귀하며, 로비 입장 시 자동으로 `"ready" = true`를 세팅하여 큐에 다시 들어간다. 유저가 레디 버튼을 다시 누를 필요가 없다.

파티의 경우, 두 명 모두 자동 레디 처리되면 파티 단위로 큐에 재진입한다.

---

## 7. 엣지 케이스 처리

### 7.1 MasterClient 이탈

큐 상태가 Room Custom Property `"matchQueue"`에 직렬화되어 있으므로, 새 MasterClient가 `OnMasterClientSwitched`에서 복원한다. 매칭 처리가 중단 없이 이어진다.

### 7.2 플레이어 접속 끊김 (큐 대기 중)

`OnPlayerLeftRoom`에서 해당 플레이어를 큐에서 제거한다. 파티원이 끊겼으면 파티 전체를 큐에서 제거하고, 남은 파트너의 파티 프로퍼티를 초기화한다.

### 7.3 레디 취소

플레이어가 `"ready" = false`를 세팅하면, MasterClient가 `OnPlayerPropertiesUpdate`에서 해당 엔트리를 큐에서 제거한다. 파티원이 레디를 취소하면 파티 전체를 큐에서 뺀다.

### 7.4 파티 상대가 로비에 없음

`PlayerList`에서 UserId를 찾지 못하면 "플레이어가 로비에 없습니다"를 표시한다. 게임 중이거나 오프라인인 경우에 해당한다.

### 7.5 로비 방 자체가 없어진 경우 (복귀 시)

`JoinRoom(lobbyRoomName)` 실패 시, `JoinRandomRoom(filter: rt="lobby")`으로 다른 로비에 합류하거나 새 로비를 생성한다. 이 경우 이전 파티 상대와 다른 로비에 들어갈 수 있으므로, 파티 프로퍼티를 초기화하고 UI에 "파티가 해제되었습니다"를 표시한다.

---

## 8. 프로퍼티 스키마

### 8.1 Room Custom Properties (로비 방)

| Key | Type | Purpose |
|---|---|---|
| `"rt"` | string | `"lobby"` — 방 타입 필터용. `CustomRoomPropertiesForLobby`에 등록. |
| `"matchQueue"` | string (JSON) | 직렬화된 큐. MasterClient 교체 시 복구용. |

### 8.2 Player Custom Properties

| Key | Type | Purpose |
|---|---|---|
| `"ready"` | bool | 매칭 대기 상태 |
| `"partyId"` | string 또는 null | 파티 식별자. 같은 값을 가진 2명이 파티. null이면 솔로. |
| `"partyMembers"` | string (JSON) | `[userId, userId]` 형태의 2인 배열. UI에서 파티 상대 표시용. |
| `"costume"` | int | 스킨/의상 인덱스. 로비에서 파티 상대 외형 표시에 사용. |
| `"lobbyRoomName"` | string | 게임 방 이동 전에 저장. 게임 종료 후 복귀용. |
| `"team"` | int | 팀 번호. 기존 `PhotonTeamManager`와 호환. |

### 8.3 Room Custom Properties (게임 방)

| Key | Type | Purpose |
|---|---|---|
| `"rt"` | string | `"game"` — 로비와 구분 |

### 8.4 커스텀 이벤트 코드

| Code | Name | Sender | Target | Content |
|---|---|---|---|---|
| 이벤트1 | PartyInvite | 초대자 | 초대 대상 1명 | `{ "fromUserId": "..." }` |
| 이벤트2 | PartyInviteResponse | 초대 대상 | 초대자 1명 | `{ "accepted": bool }` |
| 이벤트3 | PartyDisband | 해제자 | 파티 상대 1명 | `{ "reason": "leave" }` |
| 이벤트4 | MatchConfirmed | MasterClient | 매칭된 8명 | `{ "room": "G-xxxxxxxx" }` |

---

## 9. 기존 코드 변경 사항

### 9.1 폐기 대상

`PhotonPartyManager` — 별도 파티 방 생성/참가/매칭 로직 전체를 새 구조로 대체한다. 파티 방(`IsVisible=false`, `rt="party"`)을 만드는 방식은 더 이상 사용하지 않는다.

`PhotonRoomManager.JoinRandomRoom()` — 기존 솔로 랜덤 매칭 흐름을 큐 기반 매칭으로 대체한다.

기존 `LobbyManager.RequestMatch()` — 로비 방 내 큐 매니저와 연동하는 새 흐름으로 교체한다.

### 9.2 유지 대상

`PhotonTeamManager` — `AssignTeamsRandomly()`, `partyId` 기반 그룹핑, 팀 배정 로직 전체를 그대로 재사용한다.

`PhotonServerManager` — 서버 접속, 닉네임 관리 등 기존 로직을 유지한다.

`PhotonRoomTypes` — `"lobby"`와 `"game"` 타입을 추가한다. 기존 `"party"` 타입은 폐기한다.

### 9.3 신규 생성

`MatchQueueManager` — MasterClient에서 동작하는 큐 처리 매니저. 큐 진입/제거, 배치 구성, `RaiseEvent` 전송, 큐 직렬화를 담당한다.

`LobbyRoomConnector` — 앱 시작 시 로비 방 접속 흐름 (`JoinRandomRoom` → 실패 시 `CreateRoom`)을 담당한다.

`PartyManager` (신규) — 파티 초대/수락/거절/해제, 프로퍼티 세팅, 이벤트 송수신을 담당한다. 기존 `PhotonPartyManager`와 달리 별도 방을 만들지 않는다.

---

## 10. 전체 플로우 요약

```
앱 시작
  → Photon 접속
  → JoinRandomRoom(rt="lobby") 또는 CreateRoom(lobby 설정)
  → 로비 방 입장

로비 방 (모든 플레이어 상주)
  ├─ 파티 초대: UserId 입력 → PlayerList에서 검색 → RaiseEvent → 수락 시 partyId 세팅
  ├─ 레디: "ready"=true → MasterClient가 큐에 추가 (파티는 원자적 2슬롯 단위)
  └─ 매칭: MasterClient가 큐에서 8슬롯 배치 구성 → RaiseEvent로 게임 방 이름 전달

게임 방 이동
  → lobbyRoomName 저장 → LeaveRoom
  → JoinOrCreateRoom(gameRoomName)
  → 15초 타임아웃: 8명 도착 시 게임 시작, 6~7명이면 소수 시작, 5명 이하면 로비 복귀

게임 진행
  → 기존 InGame 로직 (레이스, 팀 모드 등)
  → PhotonTeamManager로 팀 배정 (partyId 기반 그룹핑 재사용)

게임 종료
  → Awards 씬
  → LeaveRoom → JoinRoom(lobbyRoomName)
  → 로비 복귀 (partyId 유지, ready=false 리셋)
  → 다시 레디 → 재매칭 가능

카운트다운 중 취소
  → 전원 로비 복귀
  → 자동 레디 (returnReason="cancelled")
  → 큐에 재진입
```
