using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AbilityAssetDatabase", menuName = "ServerSouls/Ability Asset Database")]
public class AbilityAssetDatabase : ScriptableObject
{
    [System.Serializable]
    public class SpriteMapping { public string key; public Sprite asset; }
    
    [System.Serializable]
    public class AnimMapping { public string key; public AnimationClip asset; }
    
    [System.Serializable]
    public class PrefabMapping { public string key; public GameObject asset; }

    [Header("에셋 등록소")]
    public List<SpriteMapping> icons = new List<SpriteMapping>();
    public List<AnimMapping> animations = new List<AnimMapping>();
    public List<PrefabMapping> prefabs = new List<PrefabMapping>(); // VFX, Hitbox 공용

    // 🔍 키값으로 에셋을 찾아주는 헬퍼 함수들
    public Sprite GetIcon(string key) => icons.Find(x => x.key == key)?.asset;
    public AnimationClip GetAnim(string key) => animations.Find(x => x.key == key)?.asset;
    public GameObject GetPrefab(string key) => prefabs.Find(x => x.key == key)?.asset;
}