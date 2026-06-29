using System.Collections.Generic;
using UnityEngine;

// [역할] 셰이더 예열(프리워밍)에 사용할 "이펙트 목록" 데이터.
//  - 처음 화면에 뜰 때 끊기는(셰이더 변형을 그제서야 컴파일하는) 이펙트들을 여기에 등록한다.
//  - ★ 나중에 이펙트 추가할 때: 이 에셋의 effectPrefabs 리스트에 프리팹을 드래그만 하면 끝.
//  - 생성: 프로젝트 창에서 우클릭 → Create → Optimization → Shader Warmup Library
[CreateAssetMenu(fileName = "WarmupLibrary", menuName = "Optimization/Shader Warmup Library")]
public class WarmupLibrary : ScriptableObject
{
    [Tooltip("예열할 이펙트 프리팹들. 피 이펙트(BFX), 폭발(EnergyExplosion), 보스 이펙트 등 '처음 뜰 때 끊기는' 것들을 등록.\n" +
             "새 이펙트를 추가하면 여기에 드래그만 하면 됨.")]
    public List<GameObject> effectPrefabs = new List<GameObject>();

    [Tooltip("(선택) 녹화해 둔 ShaderVariantCollection. 있으면 WarmUp()으로 한 번에 컴파일한다.\n" +
             "Project Settings → Graphics → Shader Loading 에서 추적·저장해 만들 수 있다.")]
    public ShaderVariantCollection shaderVariants;

    [Tooltip("로드된 모든 ShaderVariantCollection을 강제 예열(Shader.WarmupAllShaders). 보통은 끔.")]
    public bool warmupAllLoadedCollections = false;

    [Tooltip("프리팹 1개당 렌더할 프레임 수. 2면 대개 충분. 늘리면 더 확실하지만 로딩이 길어진다.")]
    [Min(1)] public int framesPerPrefab = 2;
}
