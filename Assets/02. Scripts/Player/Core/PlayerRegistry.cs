using System.Collections.Generic;

public static class PlayerRegistry
{
    private static readonly List<NetworkPlayerController> Players = new List<NetworkPlayerController>();

    public static IReadOnlyList<NetworkPlayerController> All => Players;
    public static NetworkPlayerController LocalPlayer { get; private set; }

    public static void Register(NetworkPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (!Players.Contains(player))
        {
            Players.Add(player);
        }

        if (player.Object != null && player.Object.HasInputAuthority)
        {
            LocalPlayer = player;
        }
    }

    public static void Unregister(NetworkPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        Players.Remove(player);

        if (LocalPlayer == player)
        {
            LocalPlayer = FindLocalPlayer();
        }
    }

    private static NetworkPlayerController FindLocalPlayer()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            NetworkPlayerController player = Players[i];
            if (player != null && player.Object != null && player.Object.HasInputAuthority)
            {
                return player;
            }
        }

        return null;
    }
}
