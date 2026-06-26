# 📢 [공지] 통합 SoundManager 사용 가이드

기존에 파편화되어 있던 사운드 재생 방식이 **`SoundManager` 하나로 완벽하게 통합**되었습니다.
이제 각 오브젝트마다 귀찮게 `AudioSource`를 붙이고 믹서를 연결할 필요가 없습니다! 스크립트에서 사운드 원본(`AudioClip`)만 들고 있다가 매니저에게 던져주기만 하면 됩니다.

## 💡 1분 요약 (이것만 알아도 됩니다)

* **UI/시스템 (어디서든 똑같이 들림):** `SoundManager.Instance.PlaySFX_2D(클립, 카테고리);`
* **인게임 (거리에 따라 작아지고 방향이 느껴짐):** `SoundManager.Instance.PlaySFX_3D(클립, 내위치, 카테고리);`
* **배경음악:** `SoundManager.Instance.PlayBGM(클립);`

---

## 🛠️ Step 1. 준비 작업 (공통)

소리를 내야 하는 스크립트(보스, 플레이어, 무기, UI 등) 상단에 `AudioClip` 변수를 하나 뚫어두고, 유니티 인스펙터에서 재생할 사운드 파일(.mp3, .wav)을 쏙 넣어주세요.

```csharp
public AudioClip attackSound; // 에디터에서 사운드 파일 할당!

```

---

## 🔊 Step 2. 3D 사운드 재생 (게임 플레이 전용)

보스 공격, 발소리, 피격음 등 **맵 특정 위치에서 나야 하는 소리**는 무조건 3D 재생을 사용합니다. 내 캐릭터가 멀어지면 소리도 자연스럽게 작아집니다.

```csharp
public AudioClip bossRoarClip;

public void OnBossRoarAnimation()
{
    // PlaySFX_3D(오디오클립, 소리가날위치, 카테고리, [개별볼륨])
    SoundManager.Instance.PlaySFX_3D(bossRoarClip, transform.position, SoundCategory.BossGimmick);
}

```

* **Tip:** 타격감이 너무 약한가요? 마지막 파라미터에 `1.5f`를 넣으면 이 소리만 1.5배 크게 재생됩니다.
`SoundManager.Instance.PlaySFX_3D(clip, transform.position, SoundCategory.CombatHit, 1.5f);`

---

## 🔉 Step 3. 2D 사운드 재생 (UI, 시스템 알림 전용)

버튼 클릭, 팝업 창, 퀘스트 완료 등 **카메라 거리에 상관없이 화면 전체에 똑같이 울려야 하는 소리**에 사용합니다.

```csharp
public AudioClip clickSound;

public void OnButtonClick()
{
    // 2D 사운드는 위치(transform.position)가 필요 없습니다!
    SoundManager.Instance.PlaySFX_2D(clickSound, SoundCategory.UI);
}

```

---

## 🎵 Step 4. 배경음악(BGM) 재생

씬이 시작될 때나 보스 페이즈가 전환될 때 사용합니다. BGM은 알아서 무한 반복(Loop)되며, 새로운 BGM을 틀면 기존 BGM은 자동으로 꺼집니다.

```csharp
public AudioClip phase2Bgm;

public void StartPhase2()
{
    SoundManager.Instance.PlayBGM(phase2Bgm);
    // 끄고 싶을 땐: SoundManager.Instance.StopBGM();
}

```

---

## 🏷️ 부록: 사운드 카테고리 목록 (`SoundCategory`)

소리를 재생할 때는 반드시 **이 소리가 어떤 종류인지** 이름표(Enum)를 붙여주셔야 합니다. 그래야 환경설정에서 타격음만 키우거나 발소리만 줄이는 믹싱 작업이 가능해집니다.

* `SoundCategory.BGM` : 배경음악
* `SoundCategory.UI` : UI 클릭, 시스템 알림
* `SoundCategory.Footstep` : 걷기, 뛰기 소리
* `SoundCategory.CombatHit` : 타격음 (칼 부딪히는 소리 등)
* `SoundCategory.CombatHurt` : 피격음 (캐릭터가 맞았을 때)
* `SoundCategory.SkillEffect` : 스킬 이펙트 발동 소리
* `SoundCategory.BossGimmick` : 보스 포효, 거대한 기믹 소리

> 카테고리가 더 필요하다면 `SoundManager.cs` 상단의 `enum SoundCategory`에 자유롭게 추가하시고, SoundManager 인스펙터에 등록해 주시면 됩니다. 볼륨 믹싱과 3D 거리는 사운드 매니저와 옵션 UI가 알아서 다 처리하니, 여러분은 **원하는 타이밍에 Play 함수만** 부르시면 됩니다! 편하게 개발하세요!