using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerController
{
    private void OnGUI()
    {
        // 슬래시(/) 키로 켜는 간단한 로컬 플레이어 디버그 패널.
        if (!_showPlayerDebug || Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        const float width = 430f;
        const float margin = 30f;
        string debugText = BuildPlayerDebugText();

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(16, 16, 14, 14)
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        float textHeight = labelStyle.CalcHeight(new GUIContent(debugText), width - 32f);
        float height = Mathf.Min(Screen.height - margin * 2f, Mathf.Max(220f, textHeight + 32f));
        Rect panelRect = new Rect(Screen.width - width - margin, margin, width, height);

        GUI.Box(panelRect, string.Empty, boxStyle);
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 12f, panelRect.width - 32f, panelRect.height - 24f), debugText, labelStyle);
    }

    private string BuildPlayerDebugText()
    {
        // 플레이 중 네트워크 액션/Animator 상태를 한 화면에서 보기 위한 임시 디버그 문자열이다.
        // 공격 중복, 선입력 큐, 액션락 문제를 빠르게 확인하는 데 사용한다.
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[Player Debug]");

        if (_playerStats == null)
        {
            builder.AppendLine("Stats : missing");
        }
        else
        {
            builder.AppendLine($"HP : {_playerStats.CurrentHealth:0} / {_playerStats.MaxHealth:0}");
            builder.AppendLine($"Stamina : {_playerStats.CurrentStamina:0} / {_playerStats.MaxStamina:0}");
            builder.AppendLine($"Defense : {_playerStats.DefenseRate * 100f:0}%");
        }

        builder.AppendLine();
        AppendNetworkActionDebug(builder);

        builder.AppendLine();
        builder.AppendLine("Skills");
        if (_abilityInventory == null || _abilityInventory.EquippedModules.Count == 0)
        {
            builder.AppendLine("- None");
            return builder.ToString();
        }

        AppendEquippedPassiveSkills(builder);
        AppendActiveSkillCooldowns(builder);
        return builder.ToString();
    }

    private void AppendNetworkActionDebug(StringBuilder builder)
    {
        builder.AppendLine("Network / Action");
        builder.AppendLine($"Authority : state={HasStateAuthority}, input={Object.HasInputAuthority}, forward={(Runner != null && Runner.IsForward)}");
        builder.AppendLine($"Action : {GetActionName(LastAction)} / id={LastActionId} / seq={ActionSequence}");
        builder.AppendLine($"Consumed : net={LastConsumedActionId}, local={_lastLocalConsumedActionId}");
        builder.AppendLine($"Lock : net={ActionAnimationLocked}({(StateActionLockType)ActionLockType}), local={_localActionAnimationLocked}({(StateActionLockType)_localActionLockType})");
        builder.AppendLine($"Combo : index={BasicAttackComboIndex}, window={ComboInputWindowOpen || _localComboInputWindowOpen}, queued={_queuedComboAttack}:{_queuedComboActionId}");

        if (animator == null)
        {
            builder.AppendLine("Animator : missing");
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        builder.AppendLine($"Animator : {GetAnimatorStateName(stateInfo.shortNameHash)} / t={stateInfo.normalizedTime:0.00} / actionTag={stateInfo.IsTag("Action")}");
        builder.AppendLine($"Move Params : speed={animator.GetFloat(MoveSpeed):0.00}, lockOn={animator.GetBool(IsLockOn)}");
    }

    private static string GetActionName(byte actionType)
    {
        return actionType switch
        {
            ActionAttack => "Attack",
            ActionParry => "Parry",
            ActionRoll => "Roll",
            ActionJump => "Jump",
            ActionJumpForward => "Jump2",
            ActionImpact => "Impact",
            ActionParryImpact => "ParryImpact",
            ActionDeath => "Death",
            _ => "None"
        };
    }

    private static string GetAnimatorStateName(int shortNameHash)
    {
        if (shortNameHash == IdleState) return "idle1";
        if (shortNameHash == Slash2State) return "slash2";
        if (shortNameHash == Slash3State) return "slash3";
        if (shortNameHash == Slash4State) return "slash4";
        return shortNameHash.ToString();
    }

    private void AppendEquippedPassiveSkills(StringBuilder builder)
    {
        bool hasPassive = false;
        foreach (PlayerAbilityModule module in _abilityInventory.EquippedModules)
        {
            if (module == null || module.IsActive)
            {
                continue;
            }

            hasPassive = true;
            builder.AppendLine($"- {module.DisplayName} : Passive");
        }

        if (!hasPassive && _abilityInventory.ActiveSlots.Count == 0)
        {
            builder.AppendLine("- None");
        }
    }

    private void AppendActiveSkillCooldowns(StringBuilder builder)
    {
        float currentTime = Runner != null ? Runner.SimulationTime : Time.time;
        for (int i = 0; i < _abilityInventory.ActiveSlots.Count; i++)
        {
            PlayerAbilitySlot slot = _abilityInventory.ActiveSlots[i];
            PlayerAbilityModule module = slot?.Module;
            if (module == null)
            {
                continue;
            }

            float remaining = Mathf.Max(0f, slot.NextReadyTime - currentTime);
            string cooldownText = remaining > 0f ? $"{remaining:0.0}s" : "Ready";
            builder.AppendLine($"- [{slot.KeyCode}] {module.DisplayName} : {cooldownText}");
        }
    }

}
