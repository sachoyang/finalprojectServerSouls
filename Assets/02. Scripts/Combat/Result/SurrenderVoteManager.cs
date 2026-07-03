using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SurrenderVoteManager : NetworkBehaviour
{
    [SerializeField] private CombatResultManager combatResultManager;

    private readonly HashSet<int> votedPlayers = new HashSet<int>();

    private bool voteInProgress;
    private int voteSessionId;
    private int expectedPlayers;
    private int requiredYesVotes;
    private int yesVotes;
    private int noVotes;

    public void RequestSurrenderFromLocalPlayer()
    {
        if (Runner == null)
            return;

        RPC_RequestSurrender(Runner.LocalPlayer);
    }

    public void SubmitLocalVote(int sessionId, bool agreed)
    {
        if (Runner == null)
            return;

        RPC_SubmitVote(Runner.LocalPlayer, sessionId, agreed);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSurrender(PlayerRef requester)
    {
        if (voteInProgress)
            return;

        ResolveReferences();

        expectedPlayers = GetActivePlayerCount();
        if (expectedPlayers <= 0)
            return;

        voteInProgress = true;
        voteSessionId++;
        yesVotes = 1;
        noVotes = 0;
        votedPlayers.Clear();
        votedPlayers.Add(requester.RawEncoded);

        requiredYesVotes = GetRequiredYesVotes(expectedPlayers);

        if (expectedPlayers <= 1 || yesVotes >= requiredYesVotes)
        {
            AcceptSurrender();
            return;
        }

        RPC_ShowSurrenderVote(voteSessionId, requester);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitVote(PlayerRef voter, int sessionId, bool agreed)
    {
        if (!voteInProgress || sessionId != voteSessionId)
            return;

        int voterKey = voter.RawEncoded;
        if (votedPlayers.Contains(voterKey))
            return;

        votedPlayers.Add(voterKey);

        if (agreed)
            yesVotes++;
        else
            noVotes++;

        if (yesVotes >= requiredYesVotes)
        {
            AcceptSurrender();
            return;
        }

        int remainingVotes = expectedPlayers - votedPlayers.Count;
        bool canStillPass = yesVotes + remainingVotes >= requiredYesVotes;

        if (!canStillPass || votedPlayers.Count >= expectedPlayers)
        {
            RejectSurrender();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowSurrenderVote(int sessionId, PlayerRef requester)
    {
        CombatQuickMenuView quickMenu = FindObjectOfType<CombatQuickMenuView>(true);
        if (quickMenu == null || Runner == null)
            return;

        if (Runner.LocalPlayer == requester)
        {
            quickMenu.ShowSurrenderWaiting();
            return;
        }

        quickMenu.ShowSurrenderVotePopup(sessionId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AcceptSurrender()
    {
        CombatQuickMenuView quickMenu = FindObjectOfType<CombatQuickMenuView>(true);
        if (quickMenu != null)
            quickMenu.CloseMenu();

        CombatResultManager resultManager =
            combatResultManager != null
                ? combatResultManager
                : FindObjectOfType<CombatResultManager>();

        if (resultManager != null)
            resultManager.RequestDefeat();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RejectSurrender()
    {
        CombatQuickMenuView quickMenu = FindObjectOfType<CombatQuickMenuView>(true);
        if (quickMenu != null)
            quickMenu.ShowVoteFailedMessage();
    }

    private void AcceptSurrender()
    {
        voteInProgress = false;
        RPC_AcceptSurrender();
    }

    private void RejectSurrender()
    {
        voteInProgress = false;
        RPC_RejectSurrender();
    }

    private int GetActivePlayerCount()
    {
        if (Runner == null)
            return 0;

        int count = 0;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    private int GetRequiredYesVotes(int playerCount)
    {
        if (playerCount <= 1)
            return 1;

        if (playerCount == 2)
            return 2;

        return 2;
    }

    private void ResolveReferences()
    {
        if (combatResultManager == null)
            combatResultManager = GetComponent<CombatResultManager>();

        if (combatResultManager == null)
            combatResultManager = FindObjectOfType<CombatResultManager>();
    }
}