# CrazyParrying — 핵심 기능 문서

## 목차
1. [게임 개요](#1-게임-개요)
2. [플레이어](#2-플레이어)
3. [물풍선](#3-물풍선)
4. [적 (Enemy)](#4-적-enemy)
5. [장애물 대포 (ObstacleCanon)](#5-장애물-대포-obstaclecanon)
6. [스테이지 / 게임 흐름](#6-스테이지--게임-흐름)
7. [오브젝트 풀](#7-오브젝트-풀)
8. [어드레서블 리소스](#8-어드레서블-리소스)

---

## 1. 게임 개요

탑다운 뷰 패링 액션 게임. 적이 던지는 물풍선을 **패링**으로 되받아쳐 적을 처치하는 것이 핵심 메카닉.

- **입력**: WASD 이동 / Space 대시 / 마우스 좌클릭 패링
- **승리 조건**: 스테이지 내 모든 적 처치
- **패배 조건**: 플레이어 HP 0

---

## 2. 플레이어

### 2-1. 이동 (`PlayerInput.MoveAction`)
- 8방향 이동, `CharacterController` 기반
- `cc.isGrounded` 여부로 중력 적용 (점프 없음)
- 대시 중(`isDashing`)에는 이동 입력 차단

### 2-2. 대시 (`DashAsync`)
- **Space** 키, `dashDuration(0.2s)` 동안 `dashSpd(10)` 속도로 마지막 이동 방향(`lastValidDir`)으로 돌진
- `dashCoolTime(1s)` 쿨다운, `Time.unscaledTime` 기준 (일시정지 무관)
- UniTask CancellationToken으로 오브젝트 파괴 시 루프 종료

### 2-3. 패링 (`TryParry` / `ParryAsync`)
| 구분 | 범위 | 효과 |
|------|------|------|
| **정밀 패링** (Precise) | 반경 `parryPreciseRadius(2.5m)`, 각도 `parryAngle(120°)` | `WaterBomb.OnParried()` — 적을 향해 발사 |
| **외곽 패링** (Deflect) | 반경 `parryOuterRadius(5m)`, 동일 각도 | `WaterBomb.OnDeflected()` — 랜덤 위치 낙하 |

- `parryCoolTime(0.8s)` 쿨다운, `Time.unscaledTime` 기준
- 클릭 시 `Physics.SyncTransforms()` → `OverlapCapsuleNonAlloc` 으로 범위 내 모든 물풍선 동시 탐지
- 캡슐 높이 `parryMaxHeight(5m)` → 높은 위치 물풍선도 판정
- 수평 거리가 거의 0인 물풍선(바로 위)은 각도 무시하고 정밀 판정
- **패링 가능 조건**: `IsParryable = gameObject.activeSelf && !hasLanded` (착지 후 불가)
- **카메라 셰이크**: 정밀 패링 성공 시 `shakeDuration(0.15s)`, `shakeMagnitude(0.08)` 진동

### 2-4. 패링 인디케이터
- `indicatorRoot`를 씬 루트에 배치, `LateUpdate`에서 플레이어 위치 동기화 (scale 영향 차단)
- 외곽 부채꼴 + 정밀 부채꼴 메시를 Init 시 1회 생성, 런타임에는 방향(`indicatorRoot.rotation`)만 회전
- 색상: 정밀 영역(초록 `colorPrecise`), 외곽 영역(파랑 `colorOuter`), 쿨다운 중(회색 `colorCooldown`)
- `indicatorFadeTime(0.5s)` 동안 알파 페이드 아웃

### 2-5. HP (`PlayerStatus`)
- `currentHp`, `MaxHp` (PlayerData ScriptableObject)
- `TakeDamage(amount)` → 히트 이펙트 재생 → HP 0 이하 시 `Die()`
- `Die()` → `UINaviPlayerHit` Hide → `InGameManager.OnGameOver()`

### 2-6. 위치 초기화 (`WarpTo`)
```
CharacterController.enabled = false
→ transform.position = pos
→ CharacterController.enabled = true
```
CharacterController가 직접 위치 변경을 차단하기 때문에 이 패턴 사용.

---

## 3. 물풍선 (`WaterBomb`)

### 3-1. 종류
| 타입 | 발사자 | 특징 |
|------|--------|------|
| `BalloonType.Enemy` | 일반 적 | 플레이어 명중 시 착지 또는 폭발, 패링 가능 |
| `BalloonType.Cannon` | ObstacleCanon | 플레이어 직격 시 데미지 + 즉시 디스폰 (착지/폭발 없음) |

### 3-2. 비행 (`LaunchArc`)
- `rb.isKinematic = true`, DOTween으로 `transform.position` 직접 제어
- `sin(π·t) * height` 포물선 보간 — 자연스러운 호(arc) 궤적
- 비행 시간: `dist / projectileSpeed` (min/max 클램프)
- 적 발사 시 낮은 호(`enemyArcHeight(2.5m)`), 일반 발사 시 높은 호(`arcHeight(8m)`)

### 3-3. 착지 (`OnLanded`)
- `hasLanded = true` → 이후 `OnTriggerEnter` 차단, `IsParryable = false`
- `dangerZoneObj` 활성화 — Fill(반투명 디스크) + Outline(LineRenderer)을 코드로 생성, 폭발 반경(`explosionRadius * 2`) 스케일
- 색상 safe(초록)→danger(빨강) DOTween 전환, 바운스 루프 애니메이션 시작
- `fuseTime` 후 `Explode()` 자동 발동
- 주변 적에게 `AlertNearbyEnemies()` 호출 (`alertRadius` 내 Enemy에 `OnBalloonDetected`)

### 3-4. 폭발 (`Explode`)
- `OverlapSphereNonAlloc` 으로 `explosionRadius` 내 타겟 탐지
- **패링된 공**: 플레이어 레이어 무시, 적만 데미지
- **미패링 공**: 적 레이어 무시, 플레이어만 데미지
- `WaterEffect` 파티클 스폰 후 `OnDispawn()`

### 3-5. 충돌 (`OnTriggerEnter`)
| 타입 | 고체 충돌 | 적 충돌 | 플레이어 충돌 |
|------|-----------|---------|--------------|
| Enemy (미패링) | StopArc → Explode | OnLanded | StopArc → Explode |
| Enemy (패링) | StopArc → Explode | 데미지 → Explode | 무시 (통과) |
| Cannon | StopArc → Explode | 무시 (통과) | 데미지 → 즉시 디스폰 |

스폰 후 0.1초(`spawnImmunityEndTime`) 동안 트리거 차단.

### 3-6. 패링 (`OnParried`)
```
StopArc() → KillAllTweens()
→ isParried = true, spawnImmunityEndTime = 0 (즉시 적 충돌 활성화)
→ balloonType = Enemy (패링 후 플레이어 통과)
→ dangerZoneObj 비활성화
→ DOPunchScale 이펙트
→ GetReflectDirection → GetLeadPredictedDirection
→ ParryLaunch(dir)
→ AlertAllEnemiesOnParry(dir)
```

**방향 계산 로직:**
1. `GetReflectDirection`: 입사 방향을 마우스 방향 법선으로 반사 후 마우스 방향과 blend
   - `combined = mouseDir + reflected`, zero 벡터 시(`sqrMagnitude < 0.01f`) → `mouseDir` 그대로 사용
2. `GetLeadPredictedDirection`: 발사 방향 앞 적에게 2회 이터레이션 리드 예측
   - 스코어 = `dot * 2 - dist * 0.05f`, 가장 높은 스코어 적 선택

**ParryLaunch:**
- `rb.isKinematic = true` 유지 (물리 전환 없음)
- 50m 목표로 DOTween arc (`sin(π·t) * 0.5f` 완만한 호)
- 적 충돌 시 `OnTriggerEnter` → 데미지 + Explode
- 50m 도달(OnComplete) 시 → Explode

### 3-7. 편향 (`OnDeflected`)
- 외곽 패링 시 호출, `isParried = true` (플레이어 통과)
- 주변 랜덤 위치(1.5~3.5m)로 낮은 호(`arcHeight * 0.4`) 낙하
- 착지 후 폭발 시 적만 데미지 (패링된 상태이므로)

### 3-8. 스폰 초기화 순서
```
OnSpawn()  → isParried/isExploded/hasLanded = false, spawnImmunityEndTime = float.MaxValue
Launch()   → KillAllTweens, 상태 초기화, spawnImmunityEndTime = Time.time + 0.1f
```
스폰 직후 0.1초간 `OnTriggerEnter` 차단 → 위치 세팅 전 오발사 방지.

---

## 4. 적 (Enemy)

### 4-1. 상태머신
```
Dead ──OnSpawn──> Idle ──탐지──> Chase ──사거리──> Attack
                  <──미탐지──                <──이탈──
Attack / Chase ──회피 이벤트──> Dodge ──완료──> Chase
```

### 4-2. 각 상태 동작
| 상태 | 동작 |
|------|------|
| **Idle** | 정지, 탐지 범위(`detectRange`) 내 플레이어 진입 시 Chase |
| **Chase** | NavMeshAgent로 플레이어 추적, 목적지 변화 0.3m 이상 시만 SetDestination |
| **Attack** | 공격 루틴 + 스트레이프(무빙샷), 사거리 이탈 시 Chase |
| **Dodge** | 물풍선 방향 수직으로 이동(좌/우 랜덤), 완료 후 Chase 복귀 |
| **Dead** | 모든 코루틴 중단, NavMeshAgent 정지 |

### 4-3. 공격 루틴 (`AttackRoutine`)
1. 이동 정지 (`agent.isStopped = true`)
2. 시야 확보 이동 (`MoveForLOS`) — 장애물 뒤면 좌우 2m/4m 오프셋 시도
3. 목표 방향 회전 (`RotateToward`) — `bodyRotateSpeed(240°/s)`
4. **리드 예측 조준**: `predicted = playerPos + playerVelocity * attackLeadTime(1.2s) + aimSpread 오차`
5. `WaterBomb` 스폰 → `Launch()` → 포신 반동 애니메이션
6. 발사 후 즉시 이동 재개 (무빙샷)

### 4-4. 무빙샷 스트레이프 (`StrafeAroundPlayer`)
- Attack 상태에서 공격 루틴 없을 때 실행
- `strafeTimer(1.5~3s)` 주기로 플레이어 주변(`attackRange * 0.6~0.9`) 랜덤 위치로 NavMesh 이동

### 4-5. 회피 시스템
- **패링 경보** (`OnParriedBalloonAlert`): 물풍선 진행 방향과 자신 방향 dot > 0.7 시 회피 시작
- **착지 경보** (`OnBalloonDetected`): 폭발 반경 내 물풍선 착지 시 회피 시작
- 회피 시 진행 중인 공격 루틴 즉시 중단 (`InterruptAndDodge`)
- `reactionTime` 대기 후 `dodgeChance` 확률로 실제 회피
- 회피 방향: 물풍선 진행 방향에 수직(`Vector3.Cross`) — 좌/우 랜덤
- `dodgeCooldown` 동안 중복 회피 방지

### 4-6. 포신 반동 (`PlayBarrelRecoil`)
- `InverseTransformDirection`으로 로컬 후방 계산
- `recoilBackDuration(0.06s)` 후방 → `recoilForwardDuration(0.14s)` 복귀 DOTween

### 4-7. 사망
- `ChangeState(Dead)` → `DeathEffect` 파티클 스폰 → 0.5s 페이드 아웃(DOFade)
- 페이드 완료 시 `gameObject.activeSelf` 확인 후 `OnDispawn()` (이중 호출 방지)
- `OnDispawn()` → `InGameManager.OnEnemyDied()` → 전체 적 처치 확인

---

## 5. 장애물 대포 (ObstacleCanon)

- 설치형 고정 오브젝트, NavMesh 없음
- **초기화**: 1프레임 대기(`InitNextFrame`) 후 Player.Instance 참조, `FireLoop()` 시작

### 발사 루프 (`FireLoop`)
- `waitInitDelay(max(fireInterval, 2s))` 후 시작, 이후 무한 루프
- 버스트당 `shotsPerBurst`발, 발사 간격 `shotDelay`
- 각 발사: `GetTargetPosition` → `RotateBarrelTo` (DOTween) → `FireAt` → 포신 리셋

### 목표 선택 (`GetTargetPosition`)
- **기본 후보**: 플레이어 예측 위치(`playerPos + velocity * predictLeadTime(1.2s)`) + `accuracy` 기반 scatter
  - `accuracy 1.0` → scatter 0 (완벽 예측), `accuracy 0.0` → scatter `nearbyScatterRadius * 2`
- 후보 검증: 장애물 겹침(`IsObstacleAt`) / 이전 발사 위치와의 간격(`minTargetSpacing(2.5m)`) 체크
- 10회 시도 실패 시 플레이어 현재 위치 폴백, 최종 실패 시 발사 취소

### 발사 (`FireAt`)
- `BalloonType.Cannon` 물풍선 스폰
- 플레이어 직격 시 데미지 + 즉시 디스폰 (착지/범위 폭발 없음)

---

## 6. 스테이지 / 게임 흐름

### 6-1. 난이도 선택 → 게임 시작
```
UIPopupDiffSettings → Setup(difficultyIdx)
→ LoadAsset<MapData>("MapData_{난이도}{스테이지}")
→ SummonMap / SummonPlayer / SummonDiffObject
```

### 6-2. 스폰 위치 배정 (`GetRandomPosInDonut`)
- 중심에서 `spawnMin(5m)` ~ `spawnMax(85m)` 도넛 영역 내 랜덤 배치
- `personalSpace(1.5m)` 겹침 체크 + `OverlapSphereNonAlloc` 물리 충돌 체크
- 30회 시도 실패 시 `Vector3.down * 100` 폴백

### 6-3. 스테이지 클리어
```
OnEnemyDied() → 적 전부 사망 확인
→ HasNextStage() 판단
    ├── 다음 스테이지 있음: UIPopupClear (중간 클리어, isLastStage: false)
    └── 마지막 스테이지: SaveRanking → FadeOut → DespawnForTransition → FadeIn → UIEnding
```

### 6-4. 전환 플래그 (`_isTransitioning`)
- `DespawnAll/DespawnForTransition` 실행 중 `OnEnemyDied()` 잘못 발화 방지
- 이 플래그가 true이면 `OnEnemyDied()` 즉시 return

### 6-5. 페이드 (`UIFadeInOut`)
- 스테이지 전환, 게임오버, 타이틀 이동 시 항상 FadeOut → 작업 → FadeIn

### 6-6. 디스폰 구분
| 함수 | 대상 |
|------|------|
| `DespawnForTransition()` | 플레이어 제외 전부 (스테이지 전환 시) |
| `DespawnAll()` | 플레이어 포함 전부 (재시작/타이틀 이동 시) |

---

## 7. 오브젝트 풀 (`PoolManager`)

### 풀 종류
| 타입 | 자료구조 | 용도 |
|------|----------|------|
| **Queue** | `Queue<ObjectPoolBase>` | 다수 인스턴스 (WaterBomb, Enemy 등) |
| **Single** | `Dictionary<string, ObjectPoolBase>` | 단일 인스턴스 (Map, Player 등) |
| **List** | `List<ObjectPoolBase>` | 인덱스 접근 필요 시 |

### 핵심 동작
- **PreWarm**: 게임 시작 전 Addressable에서 로드한 프리팹으로 사전 인스턴스화
- **InstantiateInactive**: SetActive(false) 상태로 생성 → Awake/OnEnable 제어
- **동적 확장**: `isAddSpawn = true`인 풀은 소진 시 자동 증가
- **ReturnToQueue**: `OnDispawn()` → `base.OnDispawn()` → 자동 풀 반환

### 활성 추적
- `activeQPoolDict: Dictionary<string, HashSet<ObjectPoolBase>>`
- 스테이지 전환 시 `ReleaseQPool(key)` → activeSet 복사 후 일괄 `OnDispawn()`

---

## 8. 어드레서블 리소스

### 그룹 구조
| 그룹 | 내용 |
|------|------|
| `DefaultLocalGroup` | MapData ScriptableObject (MapData_{난이도}{스테이지}) |
| `Prefab` | 게임 오브젝트 프리팹 (WaterBomb, Enemy, Player 등) |
| `UI` | UI 프리팹 |
| `Sound` | 사운드 클립 |

### 키 규칙
- MapData: `MapData_{eDifficulty}{stageNumber}` (예: `MapData_Normal1`)
- 풀 프리팹: 오브젝트명과 동일한 키 (예: `"WaterBomb"`, `"Enemy"`, `"DeathEffect"`)

---

## 부록: 주요 파라미터 참조

### 플레이어
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `moveSpd` | 10 | 이동 속도 |
| `dashDuration` | 0.2s | 대시 지속 시간 |
| `dashSpd` | 10 | 대시 속도 |
| `dashCoolTime` | 1s | 대시 쿨다운 |
| `parryPreciseRadius` | 2.5m | 정밀 패링 반경 |
| `parryOuterRadius` | 5m | 외곽 패링 반경 |
| `parryAngle` | 120° | 패링 각도 |
| `parryMaxHeight` | 5m | 패링 감지 높이 (캡슐) |
| `parryCoolTime` | 0.8s | 패링 쿨다운 |
| `shakeDuration` | 0.15s | 정밀 패링 카메라 셰이크 시간 |
| `shakeMagnitude` | 0.08 | 정밀 패링 카메라 셰이크 강도 |
| `indicatorFadeTime` | 0.5s | 패링 인디케이터 페이드 시간 |

### 물풍선
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `arcHeight` | 8m | 일반/Cannon 발사 호 높이 |
| `enemyArcHeight` | 2.5m | 적 발사 호 높이 |
| `projectileSpeed` | 15 | 비행 속도 |
| `parrySpeed` | 16 | 패링 후 비행 속도 |
| `minFlightTime` | 0.35s | 최소 비행 시간 |
| `maxFlightTime` | 2.5s | 최대 비행 시간 |

### 적 (EnemyData 기본값)
| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| `maxHp` | 3 | 최대 HP |
| `moveSpeed` | 3 | 이동 속도 |
| `detectRange` | 10m | 탐지 범위 |
| `attackRange` | 5m | 공격 범위 |
| `attackInterval` | 2s | 공격 간격 |
| `attackLeadTime` | 1.2s | 리드 예측 시간 |
| `bodyRotateSpeed` | 240°/s | 조준 회전 속도 |
| `dodgeChance` | 0.7 | 회피 확률 |
| `reactionTime` | 0.5s | 회피 반응 시간 |
| `dodgeDistance` | - | 회피 이동 거리 (EnemyData) |

### 장애물 대포 (ObstacleCannonData)
| 파라미터 | 설명 |
|----------|------|
| `shotsPerBurst` | 버스트당 발사 수 |
| `shotDelay` | 버스트 내 발사 간격 |
| `fireInterval` | 버스트 간격 |
| `barrelRotateSpeed` | 포신 회전 속도 (도/초) |
| `resetDuration` | 포신 리셋 시간 |
| `accuracy` | 0.0(랜덤)~1.0(완벽 예측) |
| `landCheckRadius` | 장애물 겹침 체크 반경 |
| `launchAngle` | 45° (포신 발사각) |
| `predictLeadTime` | 1.2s (이동 예측 시간) |
| `minTargetSpacing` | 2.5m (버스트 내 발사점 최소 간격) |
