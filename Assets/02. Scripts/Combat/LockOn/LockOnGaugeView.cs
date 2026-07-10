using UnityEngine;
using UnityEngine.UI;

public class LockOnGaugeView : MonoBehaviour
{
    [Header("Lock On Images")]
    [SerializeField] private Image minImage;
    [SerializeField] private Image fullGauge;

    [Header("Min Colors")]
    [SerializeField] private Color normalMinColor = Color.white;
    [SerializeField] private Color groggyMinColor = new Color(1f, 0.82f, 0.1f, 1f);

    [Header("Options")]
    [SerializeField] private bool hideWhenTargetHasNoBossCore = true;

    private Transform _target;
    private NetworkBossCore _boss;

    private void Awake()
    {
        if (minImage != null)
            normalMinColor = minImage.color;

        ClearTarget();
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            ClearTarget();
            return;
        }

        if (_boss == null)
            _boss = _target.GetComponentInParent<NetworkBossCore>();

        if (_boss == null)
        {
            SetVisible(!hideWhenTargetHasNoBossCore);
            SetFullGaugeAmount(0f);
            SetMinColor(normalMinColor);
            return;
        }

        UpdateGauge();
    }

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        _target = target;
        _boss = _target != null ? _target.GetComponentInParent<NetworkBossCore>() : null;

        SetVisible(_target != null);
    }

    public void ClearTarget()
    {
        _target = null;
        _boss = null;

        SetMinColor(normalMinColor);
        SetFullGaugeAmount(0f);
        SetVisible(false);
    }

    private void UpdateGauge()
    {
        bool isGroggy = _boss.CurrentState == BossState.Groggy;

        if (isGroggy)
        {
            SetMinColor(groggyMinColor);

            if (fullGauge != null)
                fullGauge.gameObject.SetActive(false);

            SetVisible(true);
            return;
        }

        SetMinColor(normalMinColor);

        float ratio = _boss.maxGroggy > 0f
            ? Mathf.Clamp01(_boss.CurrentGroggy / _boss.maxGroggy)
            : 0f;

        float remainingRatio = 1f - ratio;
        SetFullGaugeAmount(remainingRatio);
        SetVisible(true);
    }

    private void SetFullGaugeAmount(float amount)
    {
        if (fullGauge == null)
            return;

        fullGauge.gameObject.SetActive(amount > 0f);
        fullGauge.fillAmount = Mathf.Clamp01(amount);
    }

    private void SetMinColor(Color color)
    {
        if (minImage != null)
            minImage.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (minImage != null && minImage.gameObject.activeSelf != visible)
            minImage.gameObject.SetActive(visible);

        if (fullGauge != null && !visible)
            fullGauge.gameObject.SetActive(false);
    }
}