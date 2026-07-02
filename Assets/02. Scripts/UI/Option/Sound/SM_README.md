# 📢 [공지] 통합 SoundManager 사용 가이드

사운드 재생은 **`SoundManager` 하나로 통합**되어 있습니다.
각 오브젝트마다 `AudioSource`를 붙이고 믹서를 연결할 필요가 없습니다. 스크립트에서 사운드 원본(`AudioClip`)만 들고 있다가 매니저에게 던져주면 끝입니다.

## 💡 1분 요약 (이것만 알아도 됩니다)

```csharp
// UI/시스템 소리 (어디서든 똑같이 들림)
SoundManager.Instance.PlaySFX_2D(클립, SoundCategory.UI);

// 인게임 소리 (거리에 따라 작아지고 방향이 느껴짐)
SoundManager.Instance.PlaySFX_3D(클립, transform.position, SoundCategory.CombatHit);

// 배경음악 (자동 루프 + 이전 곡과 크로스페이드)
SoundManager.Instance.PlayBGM(클립);
```

---

## 🛠️ Step 1. 준비 작업 (공통)

소리를 내야 하는 스크립트(보스, 플레이어, 무기, UI 등) 상단에 `AudioClip` 변수를 하나 뚫어두고, 인스펙터에서 사운드 파일(.mp3, .wav)을 넣어주세요.

```csharp
public AudioClip attackSound; // 에디터에서 사운드 파일 할당!
```

SoundManager는 싱글톤이라 씬에 하나만 있으면 되고, 이미 세팅되어 있습니다. 여러분이 만들거나 배치할 필요 없습니다.

---

## 🔊 Step 2. 3D 사운드 재생 (게임 플레이 전용)

보스 공격, 발소리, 피격음 등 **맵의 특정 위치에서 나야 하는 소리**는 무조건 3D 재생을 사용합니다. 캐릭터가 멀어지면 소리도 자연스럽게 작아집니다.

```csharp
public AudioClip bossRoarClip;

public void OnBossRoarAnimation()
{
    // PlaySFX_3D(클립, 소리날위치, 카테고리, [개별볼륨=1f], [지연시간=0f])
    SoundManager.Instance.PlaySFX_3D(bossRoarClip, transform.position, SoundCategory.BossGimmick);
}
```

* **개별 볼륨:** 타격감이 약하면 네 번째 파라미터로 이 소리만 키울 수 있습니다.
  `PlaySFX_3D(clip, pos, SoundCategory.CombatHit, 1.5f);` → 이 소리만 1.5배
* **지연 재생:** 다섯 번째 파라미터에 초를 넣으면 그만큼 늦게 재생됩니다. 애니메이션 타이밍 맞출 때 유용.
  `PlaySFX_3D(clip, pos, SoundCategory.SkillEffect, 1f, 0.3f);` → 0.3초 뒤 재생
* 소리가 들리는 최소/최대 거리는 카테고리별로 SoundManager 인스펙터에서 일괄 관리됩니다(아래 부록 참고). 개별 호출에서 신경 쓸 필요 없음.

---

## 🔉 Step 3. 2D 사운드 재생 (UI, 시스템 알림 전용)

버튼 클릭, 팝업, 퀘스트 완료 등 **거리와 상관없이 화면 전체에 똑같이 울려야 하는 소리**에 사용합니다.

```csharp
public AudioClip clickSound;

public void OnButtonClick()
{
    // 2D는 위치가 필요 없습니다. (개별볼륨, 지연시간 파라미터는 3D와 동일하게 지원)
    SoundManager.Instance.PlaySFX_2D(clickSound, SoundCategory.UI);
}
```

---

## 🎵 Step 4. 배경음악(BGM)

씬 시작이나 보스 페이즈 전환 때 사용합니다. BGM은 자동으로 무한 반복되고, 새 BGM을 틀면 **기존 곡과 1.5초 크로스페이드**로 자연스럽게 교체됩니다. 같은 곡을 또 틀면 무시되니 매 프레임 불러도 안전합니다.

```csharp
public AudioClip phase2Bgm;

public void StartPhase2()
{
    SoundManager.Instance.PlayBGM(phase2Bgm);        // (클립, [볼륨=1f], [지연=0f])
    // 끄고 싶을 땐: SoundManager.Instance.StopBGM();
}
```

---

## ⚠️ 꼭 알아둘 주의사항

1. **멀티플레이: 사운드는 각자 컴퓨터에서만 재생됩니다.**
   `PlaySFX_3D`를 호스트에서만 부르면 호스트만 듣습니다. 모든 플레이어가 들어야 하는 소리는 **모든 클라이언트가 실행하는 코드**(예: `Render()`, 애니메이션 이벤트, 네트워크 변수 변화 감지 시점)에서 호출하세요. 보스 비주얼 스크립트들이 이 방식을 쓰고 있으니 참고.

2. **동시 재생 한도는 20개**(SFX 스피커 풀 크기)입니다. 20개가 전부 재생 중이면 새 요청은 **조용히 무시**됩니다. 한 프레임에 수십 발씩 터지는 연출은 사운드 호출을 간추리세요(예: N발마다 1회만 재생).

3. **`null` 클립은 안전하게 무시**됩니다. 클립 할당 여부를 매번 if로 검사할 필요는 없지만, 소리가 안 나면 인스펙터에 클립이 비어있는지부터 확인하세요.

4. **볼륨 옵션 슬라이더 연동**(`SetMasterVolume` / `SetBGMVolume` / `SetSFXVolume`)은 옵션 UI 전용 API입니다. 게임플레이 코드에서 부르지 마세요.

---

## 🏷️ 부록 A. 사운드 카테고리 목록 (`SoundCategory`)

재생할 때 반드시 **이 소리가 어떤 종류인지** 이름표(enum)를 붙여야 합니다. 그래야 카테고리 단위로 볼륨 믹싱/거리 조절이 됩니다.

| 카테고리 | 용도 |
|---|---|
| `BGM` | 배경음악 |
| `UI` | UI 클릭, 시스템 알림 |
| `Footstep` | 걷기, 뛰기 소리 |
| `CombatHit` | 타격음 (칼 부딪히는 소리 등) |
| `CombatHurt` | 피격음 (캐릭터가 맞았을 때) |
| `SkillEffect` | 스킬 발동 소리 |
| `BossGimmick` | 보스 포효, 거대 기믹 소리 |

**카테고리 추가 방법:** `SoundManager.cs` 상단의 `enum SoundCategory`에 항목을 추가하고, 씬의 SoundManager 인스펙터 → "카테고리별 사운드 설정" 리스트에 등록하면 끝. 등록 안 해도 기본값(볼륨 1배, 거리 5~30m)으로 동작은 합니다.

각 카테고리 설정값:
* **volumeMultiplier** — 이 카테고리 전체 볼륨 배율 (0~2)
* **minDistance / maxDistance** — 3D 재생 시 감쇠 시작/소멸 거리 (미터)

---

## 🔍 부록 B. 디버깅 팁 (에디터 전용)

플레이 중 SoundManager 인스펙터를 열면 **"👀 모니터링"** 섹션에 현재 재생 중인 BGM과 모든 SFX가 실시간으로 보입니다.

* `speaker` 클릭 → 하이라키에서 소리 나는 스피커 위치로 이동
* `clip` 클릭 → 프로젝트 창에서 원본 오디오 파일로 이동

"이 소리 어디서 나는 거야?" 할 때 여기서 바로 찾으면 됩니다. (빌드에서는 자동으로 제거되는 기능이라 성능 걱정 없음)
