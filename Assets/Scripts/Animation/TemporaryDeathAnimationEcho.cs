using UnityEngine;

/// <summary>
/// Visual-only death echo. It has no collider, health or enemy identity and is
/// destroyed after the non-looping temporary death sequence reaches its end.
/// </summary>
[DisallowMultipleComponent]
public sealed class TemporaryDeathAnimationEcho : MonoBehaviour
{
    private bool initialized;
    private float destroyAt;

    public void Initialize(float lifetime)
    {
        destroyAt = Time.time + Mathf.Max(0.05f, lifetime);
        initialized = true;
    }

    private void Update()
    {
        if (initialized && Time.time >= destroyAt)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (initialized)
        {
            CombatAnimationDiagnostics.RecordDeathEchoCompleted();
        }
    }
}
