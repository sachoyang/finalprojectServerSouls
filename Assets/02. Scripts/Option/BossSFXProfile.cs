using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossSFXProfile", menuName = "Boss/SFX Profile")]
public class BossSFXProfile : ScriptableObject
{
    [SerializeField] private List<BossSFXEntry> entries = new List<BossSFXEntry>();

    public BossSFXEntry GetEntry(AnimationClip animationClip)
    {
        if (animationClip == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            BossSFXEntry entry = entries[i];

            if (entry != null && entry.animationClip == animationClip)
                return entry;
        }

        return null;
    }
}

[Serializable]
public class BossSFXEntry
{
    public AnimationClip animationClip;
    public List<BossSFXClipData> clips = new List<BossSFXClipData>();
}

[Serializable]
public class BossSFXClipData
{
    public AudioClip clip;
    public float delay;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
}