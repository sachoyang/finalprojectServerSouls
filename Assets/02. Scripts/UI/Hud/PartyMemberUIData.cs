// 파티 HUD와 네임플레이트에서 공통으로 사용할 수 있는 표시용 데이터다.
// 실제 생존/다운/체력 판정은 PlayerStats 같은 네트워크 컴포넌트에서 끝난 값을 읽어온다.
public readonly struct PartyMemberUIData
{
    public readonly int PlayerKey;
    public readonly string DisplayName;
    public readonly float CurrentHealth;
    public readonly float MaxHealth;
    public readonly float CurrentStamina;
    public readonly float MaxStamina;
    public readonly bool IsAlive;
    public readonly bool IsDowned;
    public readonly bool IsLocalPlayer;

    public PartyMemberUIData(
        int playerKey,
        string displayName,
        float currentHealth,
        float maxHealth,
        float currentStamina,
        float maxStamina,
        bool isAlive,
        bool isDowned,
        bool isLocalPlayer)
    {
        PlayerKey = playerKey;
        DisplayName = displayName;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
        IsAlive = isAlive;
        IsDowned = isDowned;
        IsLocalPlayer = isLocalPlayer;
    }

    public float HealthRatio => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;
    public float StaminaRatio => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;
}
