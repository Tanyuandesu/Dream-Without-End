using UnityEngine;

/// <summary>
/// 建立／設定主相機，並平滑跟隨當前玩家。
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraManager : MonoBehaviour
{
    [Header("相機")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float orthographicSize = 8f;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private Color backgroundColor =
        new Color(0.035f, 0.035f, 0.055f);

    private Transform target;
    private Vector3 velocity;

    private void Awake()
    {
        EnsureCameraExists();
    }

    public void SetTarget(Transform newTarget)
    {
        EnsureCameraExists();
        target = newTarget;

        if (target != null)
        {
            targetCamera.transform.position =
                new Vector3(
                    target.position.x,
                    target.position.y,
                    -10f);
        }
    }

    private void LateUpdate()
    {
        if (target == null || targetCamera == null)
        {
            return;
        }

        Vector3 destination = new Vector3(
            target.position.x,
            target.position.y,
            -10f);

        targetCamera.transform.position =
            Vector3.SmoothDamp(
                targetCamera.transform.position,
                destination,
                ref velocity,
                smoothTime);
    }

    private void EnsureCameraExists()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            GameObject cameraObject =
                new GameObject("Main Camera");

            cameraObject.tag = "MainCamera";
            targetCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        targetCamera.orthographic = true;
        targetCamera.orthographicSize =
            Mathf.Max(1f, orthographicSize);
        targetCamera.backgroundColor = backgroundColor;

        Vector3 position = targetCamera.transform.position;
        position.z = -10f;
        targetCamera.transform.position = position;
    }
}
