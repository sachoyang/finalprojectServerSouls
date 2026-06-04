using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DragonArenaGimmick : NetworkBehaviour
{
    public static DragonArenaGimmick Instance { get; private set; }

    [Header("기믹 오브젝트 설정")]
    [Tooltip("2페이즈 시작 시 서서히 꺼질 조명들의 부모 객체 (예: castlelight)")]
    public GameObject[] gimmickOffObjects;
    
    [Tooltip("2페이즈 시작 시 켜질 제단(도깨비불) 오브젝트들")]
    public GameObject[] gimmickOnObjects;

    [Header("조명 연출 설정")]
    [Tooltip("조명이 완전히 꺼지거나 켜지는 데 걸리는 시간 (초 단위)")]
    public float fadeDuration = 2.0f;

    private int _destroyedAltarCount = 0;
    private NetworkBossCore _targetBoss;

    // 조명의 원본 밝기를 기억해 둘 내부 클래스
    private class LightData
    {
        public Light lightComp;
        public float originalIntensity;
    }
    
    // 관리할 모든 조명 리스트
    private List<LightData> _managedLights = new List<LightData>();
    
    // 현재 실행 중인 페이드 코루틴 (중복 실행 방지용)
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        Instance = this;
        CacheLights();
    }

    // 시작할 때 부모 밑에 있는 모든 조명을 찾아서 원본 밝기를 저장해 둡니다.
    private void CacheLights()
    {
        _managedLights.Clear();
        foreach (var obj in gimmickOffObjects)
        {
            if (obj != null)
            {
                // 자식들 중에 꺼져있는(Inactive) 녀석들까지 포함해서 Light 컴포넌트를 싹 다 긁어옴
                Light[] lights = obj.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    _managedLights.Add(new LightData { lightComp = l, originalIntensity = l.intensity });
                }
            }
        }
        Debug.Log($"[Gimmick] 총 {_managedLights.Count}개의 조명을 캐싱했습니다.");
    }

    // 보스가 2페이즈에 진입할 때 호출할 함수
    public void StartGimmick(NetworkBossCore boss)
    {
        _targetBoss = boss;
        _destroyedAltarCount = 0;

        // 기존에 돌아가던 조명 코루틴이 있다면 정지
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        
        // 🔥 조명 끄기 -> 대기 -> 제단 켜기 순서를 관리하는 '시퀀스 코루틴'을 실행합니다.
        _fadeCoroutine = StartCoroutine(GimmickActivationSequence());
    }

    // ==========================================
    // 2페이즈 시작 연출 시퀀스
    // ==========================================
    private IEnumerator GimmickActivationSequence()
    {
        Debug.Log("[Gimmick] 2페이즈 연출 시작! 조명 서서히 OFF...");

        // 1. 조명 서서히 끄기 (FadeLights 코루틴이 완전히 끝날 때까지 여기서 알아서 기다립니다!)
        yield return StartCoroutine(FadeLights(0f));

        // 2. 불이 다 꺼지고 나서 0.5초의 숨 막히는 정적 (원하시면 1.0f 등으로 늘려도 됩니다)
        yield return new WaitForSeconds(0.5f);

        // 3. 제단 팟! 하고 켜기
        foreach (var obj in gimmickOnObjects) 
        { 
            if(obj != null) obj.SetActive(true); 
        }
        
        Debug.Log("[Gimmick] 조명 OFF 완료. 0.5초 후 제단 ON!");
    }

    public void OnAltarDestroyed()
    {
        _destroyedAltarCount++;
        Debug.Log($"[Gimmick] 제단 파괴됨! ({_destroyedAltarCount} / {gimmickOnObjects.Length})");

        if (_destroyedAltarCount >= gimmickOnObjects.Length)
        {
            ClearGimmick();
            
            if (_targetBoss != null)
            {
                float damage = _targetBoss.maxHP * 0.1f;
                _targetBoss.RPC_TakeDamage(damage, _targetBoss.maxGroggy);
            }
        }
    }

    public void ClearGimmick()
    {
        // 1. 남은 제단(파괴된 잔해) 끄기
        foreach (var obj in gimmickOnObjects) { if(obj != null) obj.SetActive(false); }

        // 2. 조명 서서히 원래 밝기로 켜기 (목표 밝기 배율 = 1f)
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeLights(1f));

        if (_targetBoss != null)
        {
            _targetBoss.IsGimmickActive = false;
        }

        Debug.Log("[Gimmick] 기믹 종료! 조명 서서히 복구 완료.");
    }

    // ==========================================
    // 🔥 조명 밝기를 부드럽게 조절하는 핵심 코루틴
    // ==========================================
    private IEnumerator FadeLights(float targetMultiplier)
    {
        float elapsed = 0f;
        
        // 조명이 꺼지던 도중에 기믹이 풀렸을 때 튀는 현상을 막기 위해, '현재 밝기'를 시작점으로 잡음
        float[] startMultipliers = new float[_managedLights.Count];
        for (int i = 0; i < _managedLights.Count; i++)
        {
            if (_managedLights[i].lightComp != null && _managedLights[i].originalIntensity > 0)
            {
                startMultipliers[i] = _managedLights[i].lightComp.intensity / _managedLights[i].originalIntensity;
            }
        }

        // 설정한 시간(fadeDuration) 동안 서서히 밝기 조절
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration; // 0.0 ~ 1.0

            for (int i = 0; i < _managedLights.Count; i++)
            {
                if (_managedLights[i].lightComp != null)
                {
                    // 현재 밝기 배율에서 목표 배율(0 또는 1)로 스르륵 이동
                    float currentMult = Mathf.Lerp(startMultipliers[i], targetMultiplier, t);
                    _managedLights[i].lightComp.intensity = _managedLights[i].originalIntensity * currentMult;
                }
            }
            yield return null; // 다음 프레임까지 대기
        }

        // 시간이 다 지나면 오차 없이 100% 목표값으로 딱 맞춰줌
        for (int i = 0; i < _managedLights.Count; i++)
        {
            if (_managedLights[i].lightComp != null)
            {
                _managedLights[i].lightComp.intensity = _managedLights[i].originalIntensity * targetMultiplier;
            }
        }
    }
}