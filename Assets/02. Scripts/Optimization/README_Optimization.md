# 최적화 시스템 (이펙트 풀 + 로딩 예열)

이펙트가 처음 뜰 때 생기는 **끊김(로딩 걸림)** 을 없애기 위한 시스템입니다.
원인은 두 가지였습니다.

1. **셰이더 변형 런타임 컴파일** — 커스텀 셰이더(피 이펙트 BFX, 보스 Distortion 등)를 처음 렌더할 때 그제서야 컴파일.
2. **매번 `Instantiate`/`Destroy`** — 첫 생성 시 텍스처/메시 GPU 업로드 + GC 발생.

해결: **로딩 시점에 미리 다 만들어/컴파일해 두고(예열·풀 사전생성)**, 게임 중에는 풀에서 꺼내 재사용.

---

## 폴더 구성 (`02. Scripts/Optimization/`)

| 파일 | 역할 |
|---|---|
| `EffectPoolManager.cs` | 카테고리 기반 이펙트 풀(지속 싱글톤). **Get/Spawn/Return** |
| `EffectPoolConfig.cs` | 풀 설정 ScriptableObject. **카테고리/프리팹/한도** 정의 |
| `PooledInstance.cs` | 풀 인스턴스 표식(자동 부착). 직접 안 씀 |
| `AutoReturnToPool.cs` | `Spawn()`한 이펙트 자동 회수(파티클 종료/시간). 직접 안 씀 |
| `WarmupLibrary.cs` | 셰이더 예열용 이펙트 목록 |
| `ShaderWarmupRunner.cs` | 셰이더 예열 코어(프리팹 렌더로 변형 컴파일) |
| `LoadingSceneController.cs` | scLoading 컨트롤러(전환/오버레이 자동) |
| `LoadingRouter.cs` / `LobbyPreloadCover.cs` / `ShaderWarmupBootstrap.cs` | 로딩 진입/배치 헬퍼 |

---

## 1) 이펙트 풀 — 어떤 것을 넣나

**넣어야 하는 것: 게임 중 `Instantiate`로 자주 생성되는 짧은 비주얼 이펙트.**

| 카테고리(예시) | 들어갈 것 |
|---|---|
| `BloodEffect` | 피격 피 튐, 데칼(피 자국) |
| `BossEffect` | 보스 패턴 이펙트(폭발 EnergyExplosion, 얼음창, 브레스, 충격파 등) |
| `PlayerEffect` | 플레이어 스킬/타격/버프 이펙트 |
| `HitEffect`, `DamageText` … | 필요 시 자유롭게 추가 |

> 카테고리는 **자유롭게 추가**할 수 있습니다(설정 에셋에 줄만 추가). 코드 수정 불필요.

**넣지 말아야 하는 것**
- **NetworkObject**(Fusion이 스폰하는 것) — Fusion의 자체 풀링(`INetworkObjectProvider`)을 써야 함. 이 풀은 **로컬 비주얼 전용**.
- 씬에 1개만 있는 영구 오브젝트, 매니저, UI 등.
- 매우 드물게(전투당 1회 미만) 생성되는 것 — 풀링 이득이 적음.

---

## 2) 설정하기

1. Project 우클릭 → **Create → Optimization → Effect Pool Config** 로 `EffectPoolConfig` 에셋 생성.
2. `categories`에 카테고리 추가. 각 카테고리:
   - **name**: 예) `BossEffect`
   - **maxActive**: 동시 활성 최대 개수. 초과 시 **가장 오래된 것 자동 회수**. (예: 피 이펙트 30, 보스 이펙트 12) `0`=무제한
   - **prefabs**: 이 카테고리의 이펙트들
     - **prefab**: 이펙트 프리팹
     - **prewarmCount**: 로딩 때 미리 만들 개수(평소 동시 사용량 정도)
     - **autoReturnAfter**: `Spawn()` 자동 회수 시간(초). `0`이면 **파티클이 끝나면 자동 회수**
3. **풀매니저 배치 — Resources 자동 생성 방식(권장, 씬마다 안 넣어도 됨):**
   - `EffectPoolManager` 컴포넌트가 붙고 `config`가 연결된 **프리팹**을 만들어 **`Resources/EffectPoolManager.prefab`** 에 둔다.
   - 게임 시작 시(`RuntimeInitializeOnLoadMethod`) 자동으로 1개 생성되고 `DontDestroyOnLoad`로 유지된다.
   - → **로비에서 시작하든, 디버그로 전투씬에서 바로 시작하든** 항상 존재한다. 씬마다 배치할 필요 없음.
   - 옵션: `Persist Across Scenes` ON, `Register Loading Hook` ON.
   - (대안) 자동 생성을 안 쓰려면, **시작 진입 씬마다**(로비 + 디버그로 시작하는 scServer_stage·Gothic_Stage) 직접 1개씩 배치해도 된다. 싱글톤 가드가 중복을 막는다.

---

## 3) 사용법 (코드)

```csharp
// 가장 흔한 경우: 한 방 터지고 사라지는 이펙트 → 자동 회수
EffectPoolManager.Instance.Spawn(bloodPrefab, hitPos, hitRot);

// 부모에 붙이거나, 자동 회수가 곤란해 직접 제어할 때
var go = EffectPoolManager.Instance.Get(effectPrefab, pos, rot, parent);
// ... 다 쓰면 반드시 반환:
EffectPoolManager.Instance.Return(go);
// 또는 인스턴스에서:  go.GetComponent<PooledInstance>().ReturnToPool();
```

**기존 코드 교체 패턴**
```csharp
// 변경 전
Instantiate(bloodPrefab, pos, rot);
// 변경 후
EffectPoolManager.Instance.Spawn(bloodPrefab, pos, rot);
```
> `Destroy(fx, time)` 로 정리하던 것도 → `Spawn()`이면 자동 회수, 또는 `autoReturnAfter`로 대체.

**주의**
- 풀 인스턴스는 **재사용**된다. `OnEnable`에서 상태를 초기화하도록 작성(누적 상태 금지).
- 파티클은 풀이 `Get` 시 `Clear+Play`, `Return` 시 `Stop+Clear` 해준다.
- 풀에 등록 안 된 프리팹을 `Get`하면 `Unsorted`(무제한)로 동적 등록되고 경고가 뜬다 → config에 넣을 것.

---

## 4) 로딩(예열)과의 연결

- `EffectPoolManager`가 켜져 있으면 **로딩 화면에서 `PrewarmRoutine`이 자동 실행**되어 `prewarmCount`만큼 미리 만든다(프레임 분산).
- 동시에 `WarmupLibrary`에 등록된 프리팹으로 **셰이더 변형도 예열**한다.
- 즉, 로딩 한 번이면 **셰이더 컴파일 + 풀 채우기**가 끝나 게임 중 끊김이 사라진다.

> `WarmupLibrary`(셰이더 예열 목록)와 `EffectPoolConfig`(풀 목록)는 **겹쳐도 됨**. 보통 풀에 넣는 이펙트는 셰이더 예열 목록에도 같이 넣는다.

---

## 5) 새 이펙트 추가 체크리스트 (작업자용)

1. 이펙트 프리팹 준비.
2. `EffectPoolConfig`의 알맞은 카테고리 `prefabs`에 추가 → `prewarmCount`(평소 동시 사용량), `autoReturnAfter`(또는 0) 설정.
3. (커스텀 셰이더/처음 뜰 때 끊기면) `WarmupLibrary.effectPrefabs`에도 드래그.
4. 생성 코드에서 `Instantiate(...)` → `EffectPoolManager.Instance.Spawn(...)` 으로 교체.
5. 프리팹 스크립트가 상태를 들고 있으면 `OnEnable`에서 초기화하도록 확인(재사용 대비).

---

## 로딩 화면 구성 요소 (정리)
- **scLoading + LoadingSceneController**: 예열 + 풀 사전생성을 하는 화면. 로비(LobbyPreloadCover, additive 오버레이) / 디버그(DebugQuickEntry, additive 오버레이) / 비네트워크 전환(LoadingRouter, single)에서 사용. `minDisplayTime`으로 최소 노출 보장.
- **TransitionLoadingScreen**: 네트워크 씬 전환(방장 Start → runner.LoadScene)을 가리는 지속 커버. Fusion `OnSceneLoadStart/Done`을 받아 호스트·클라 자동. 자동 생성(Resources 프리팹 있으면 사용, 없으면 코드 단색 커버). 별도 셋업 불필요.

## 보스 이펙트 풀 적용 현황 (인수인계)

스폰은 `EffectPoolManager.SpawnPooled(prefab, pos, rot, parent)` 로 통일. (풀 없으면 자동 폴백 생성)

**✅ 풀 적용 완료**
| 위치 | 이펙트 |
|---|---|
| `DragonVisual.SpawnJumpSlamEffect` | jumpSlamEffectPrefab (점프 슬램 폭발) |
| `DragonVisual.SpawnFireEffect` | fireBreath (입에 붙는 브레스, parent 지정) |
| `PolarDragonVisual.SpawnFrozenBreath` | spreadFrozenBreathPrefab (브레스, parent 지정) |
| `IceLanceProjectile.Explode` | explosionPrefab = EnergyExplosion (얼음창 폭발) |

- 공통: `BossAoEAttack`가 붙은 것들은 스폰 후 `Initialize(배율)` 호출 유지.
- **풀 재사용 버그 수정됨**: 기존엔 판정 끝나면 `this.enabled=false`로 자신을 꺼서, 풀에서 다시 꺼낼 때 `OnEnable`이 안 와 데미지가 죽었다. → `_finished` 플래그로 멈추고 `OnEnable`에서 매번 초기화하도록 변경(`BossAoEAttack.cs`). 이제 풀로 몇 번을 재사용해도 데미지 정상.
- 자동 회수: `Spawn`이 `AutoReturnToPool`을 붙여 **파티클이 끝나면 풀로 반환**.

**⚠️ 아직 풀 미적용 (그대로 `Instantiate`) — 이유와 할 일**

> **중요 사실 확인됨**: 프로젝트에 **`ProjectileMover`를 쓰는 프리팹은 없다**(grep 결과 0개). `poisonDaggerPrefab`은 보스에 **할당 안 됨**(미사용). `spitFrozenBallPrefab`은 실제로 **`icelance_one.prefab`** 이며, 이동/소멸은 **파티클 충돌 + `IceLanceProjectile`(`Destroy(transform.root)`)** 가 담당한다(ProjectileMover 아님).

| 위치 | 이펙트 | 왜 보류 | 적용하려면 |
|---|---|---|---|
| `PolarDragonVisual.SpawnFrozenBall` / `SpawnIceLance` | icelance_one (얼음창) | `IceLanceProjectile`가 TinyShards 자식에서 `Destroy(transform.root)`로 수명 관리. 상태(_armed/_hasImpact/_exploded) 리셋 필요 | `IceLanceProjectile`를 풀 인식화(Destroy 대신 `PooledInstance.ReturnToPool`)하고 `OnEnable`에서 상태 리셋한 뒤 교체. **단, 현재 잘 동작 중이라 리스크 있어 보류** |
| `OrcAssassinVisual.ThrowPoisonDagger` | poisonDagger | 프리팹 **미할당**(미사용) | 사용 시점에 프리팹/이동방식 확인 후 교체 |
| `DragonVisual.SpawnJumpWarning` | jumpWarningPrefab | `WarningIndicator` 자체 수명 | WarningIndicator를 풀 인식으로 바꾼 뒤 교체 |

**🚫 피 이펙트(KriptoFX BFX)**: **다른 작업자 담당 — 이 작업에서 건드리지 않음.** 풀 적용 시 같은 패턴(`SpawnPooled`) 권장.

**작업자 체크리스트 (적용한 4개 프리팹)**
1. 각 프리팹을 `EffectPoolConfig`의 **BossEffect** 카테고리에 등록(`prewarmCount` 적당히).
2. 각 프리팹 루트 파티클 **Stop Action = None** 확인(Destroy면 풀이 깨짐).
3. 재사용 대비: 프리팹 스크립트가 상태를 들고 있으면 `Initialize`/`OnEnable`에서 초기화.

## 로딩 화면 구성 요소 (정리)
- **scLoading + LoadingSceneController**: 예열 + 풀 사전생성 화면 (로비/디버그 오버레이, 비네트워크 전환). `minDisplayTime` 최소 노출.
- **TransitionLoadingScreen**: 네트워크 씬 전환(방장 Start) 가림. 자동 동작/자동 생성.

## 셰이더 컴파일 / 빌드 최적화 (현재 설정)

런타임 이펙트 끊김의 핵심 원인은 **셰이더 변형 컴파일**. 두 가지를 구분할 것:
- **런타임 예열** = ShaderVariantCollection(SVC)을 미리 컴파일. SVC: `Assets/10. Renderings/NewShaderVariants.shadervariants`.
- **빌드 컴파일 시간** = 빌드에 포함되는 변형 전체. SVC와 무관, **스트리핑/포함 변형**이 결정.

**현재 적용 상태**
- SVC는 **`Project Settings → Graphics → Preloaded Shaders`(m_PreloadedShaders)** 에만 등록(앱 시작 시 자동 예열). `WarmupLibrary.shaderVariants`는 **비움** — `WarmUp()`이 동기 블로킹이라 중복/프리즈 유발했음.
- `m_PreloadShadersBatchTimeLimit: 16` (프레임 분산, 논블로킹). `EditorSettings.m_AsyncShaderCompilation: 1` (에디터 on-demand 백그라운드 컴파일).
- **빌드 스트리핑 켬**: `m_FogStripping: 1`(Automatic), `m_LightmapStripping: 1`(Automatic), URP Global `m_StripUnusedPostProcessingVariants: 1` (+ `m_StripUnusedVariants`는 원래 1).
  - ⚠️ Automatic 포그/라이트맵 스트리핑은 **Build Settings의 씬 기준**으로 유지 변형 결정. 새 게임플레이 씬은 반드시 Build Settings에 추가.
- 품질 Low/Medium/High 모두 기본 URP_Medium 사용(레벨별 오버라이드 없음). TerrainLit 쓰는 머티리얼 0개.

**SVC 재녹화(슬림화) 방법**: `Project Settings → Graphics → Shader Loading → Clear` → 실제 게임 플레이(보스/이펙트 다 발동) → `Save to asset`. (빌드 시간엔 영향 없음, 런타임 예열만)

## TODO (다음 작업 — 단계별)
- [x] 로딩 흐름: **최소 표시시간** + 방장 Start 네트워크 전환 커버.
- [x] 보스 이펙트(정적/머리부착 AoE + 얼음창 폭발) 풀 교체. BossAoEAttack 풀 재사용 버그 수정.
- [x] 셰이더 preload 단일화(PreloadedShaders) + 빌드 스트리핑 + 에디터 Async.
- [x] Gothic_Stage를 Build Settings에 추가(Fusion SceneRef 에러 해결).
- [ ] **빌드 재실행해서 빌드 시간/변형 수 감소 확인** (URP Global `Shader Variant Log Level`로 로그 확인 가능).
- [ ] (선택) SVC 슬림화: 재녹화 또는 SSAO/DBufferClear 제거(해당 렌더러 기능 미사용 시).
- [ ] (선택) 투사체/경고표식 풀 교체 — 단, **ProjectileMover 쓰는 프리팹 없음**. "투사체"=icelance_one(IceLanceProjectile, Destroy(transform.root)). 풀링하려면 IceLanceProjectile를 풀 반환형으로 + OnEnable 상태리셋. 현재 잘 동작 중이라 보류.
- [ ] 피 이펙트 풀 교체 — **다른 작업자 담당**.
- [ ] WarmupLibrary effectPrefabs에 icelance_one 중복 1개 제거.

## 다음 세션 이어가기 메모
- 미해결 핵심: **빌드 재측정**(스트리핑 효과 확인)이 다음 1순위.
- 코드/설정 모두 `Assets/02. Scripts/Optimization/`(코드) + ProjectSettings(Graphics/Editor) + `Assets/10. Renderings/`(URP·SVC)에 있음.
