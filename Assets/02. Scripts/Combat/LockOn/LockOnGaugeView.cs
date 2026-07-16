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
    [SerializeField] private bool hideMinWhenTargetHasNoBossCore = false;

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
            UpdateNonBossTarget();
            return;
        }

        UpdateBossTarget();
    }

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        _target = target;
        _boss = _target != null ? _target.GetComponentInParent<NetworkBossCore>() : null;

        if (_target == null)
        {
            ClearTarget();
            return;
        }

        SetMinVisible(true);
    }

    public void ClearTarget()
    {
        _target = null;
        _boss = null;

        SetMinColor(normalMinColor);
        SetMinVisible(false);
        SetFullGaugeVisible(false);
    }

    private void UpdateNonBossTarget()
    {
        SetMinVisible(!hideMinWhenTargetHasNoBossCore);
        SetMinColor(normalMinColor);
        SetFullGaugeVisible(false);
    }

    private void UpdateBossTarget()
    {
        SetMinVisible(true);

        bool isGroggy = _boss.CurrentState == BossState.Groggy;

        if (isGroggy)
        {
            SetMinColor(groggyMinColor);
            SetFullGaugeVisible(false);
            return;
        }

        SetMinColor(normalMinColor);

        float ratio = _boss.maxGroggy > 0f
            ? Mathf.Clamp01(_boss.CurrentGroggy / _boss.maxGroggy)
            : 0f;

        // 누적된 그로기 수치를 '그대로' 채워서 보여준다. (그로기가 max에 가까울수록 게이지도 꽉 참)
        // 예전엔 1-ratio(남은 저항량)로 그려서, 그로기가 유지된 상태인데도 게이지가 사라져
        // '0처럼' 보이던 표시 버그를 바로잡는다.
        SetFullGaugeVisible(ratio > 0f);
        SetFullGaugeAmount(ratio);
    }

    private void SetMinVisible(bool visible)
    {
        if (minImage != null && minImage.gameObject.activeSelf != visible)
            minImage.gameObject.SetActive(visible);
    }

    private void SetFullGaugeVisible(bool visible)
    {
        if (fullGauge != null && fullGauge.gameObject.activeSelf != visible)
            fullGauge.gameObject.SetActive(visible);
    }

    private void SetFullGaugeAmount(float amount)
    {
        if (fullGauge != null)
            fullGauge.fillAmount = Mathf.Clamp01(amount);
    }

    private void SetMinColor(Color color)
    {
        if (minImage != null)
            minImage.color = color;
    }
}