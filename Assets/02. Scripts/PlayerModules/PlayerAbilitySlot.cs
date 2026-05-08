using System;
using UnityEngine;

[Serializable]
public class PlayerAbilitySlot
{
    [SerializeField] private PlayerAbilityModule module;
    [SerializeField] private KeyCode keyCode;
    [SerializeField] private float nextReadyTime;

    public PlayerAbilityModule Module => module;
    public KeyCode KeyCode => keyCode;
    public float NextReadyTime => nextReadyTime;

    public PlayerAbilitySlot(PlayerAbilityModule module, KeyCode keyCode)
    {
        this.module = module;
        this.keyCode = keyCode;
    }

    public void SetKey(KeyCode newKey)
    {
        keyCode = newKey;
    }

    public bool IsReady(float currentTime)
    {
        return currentTime >= nextReadyTime;
    }

    public void StartCooldown(float currentTime)
    {
        nextReadyTime = currentTime + Mathf.Max(0f, module != null ? module.CooldownSeconds : 0f);
    }
}
