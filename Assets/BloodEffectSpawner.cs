using UnityEngine;

public class BloodEffectSpawner : MonoBehaviour
{
    [SerializeField] private GameObject bloodEffectPrefab;
    [SerializeField] private float destroyDelay = 5f;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    public void SpawnBlood(Vector3 position, Vector3 direction)
    {
        if (bloodEffectPrefab == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject blood = Instantiate(
            bloodEffectPrefab,
            position + spawnOffset,
            rotation
        );

        Destroy(blood, destroyDelay);
    }
}