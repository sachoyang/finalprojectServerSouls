// PlayerStats의 네트워크 상태를 HUD 표시용으로 옮긴 읽기 전용 데이터다.
// UI는 이 값을 그리기만 하고 체력/스태미나 같은 게임 상태를 직접 수정하지 않는다.
public readonly struct PlayerHUDData
{
    public readonly float CurrentHealth;
    public readonly float MaxHealth;
    public readonly float CurrentStamina;
    public readonly float MaxStamina;
    public readonly bool IsDead;

    public PlayerHUDData(
        float currentHealth,
        float maxHealth,
        float currentStamina,
        float maxStamina,
        bool isDead)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
        IsDead = isDead;
    }
}
