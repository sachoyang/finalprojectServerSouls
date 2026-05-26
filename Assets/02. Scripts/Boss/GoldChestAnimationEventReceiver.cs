using System;
using UnityEngine;

public class GoldChestAnimationEventReceiver : MonoBehaviour
{
    public event Action BoxOpenEnded;

    public void BoxOpenEndEvent()
    {
        BoxOpenEnded?.Invoke();
    }
}
