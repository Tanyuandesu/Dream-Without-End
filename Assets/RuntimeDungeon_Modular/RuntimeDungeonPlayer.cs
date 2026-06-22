using UnityEngine;

/// <summary>
/// 示範用玩家移動。
/// 正式專案中可以替換成你自己的角色控制器。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class RuntimeDungeonPlayer : MonoBehaviour
{
    private Rigidbody2D body;
    private Vector2 input;
    private float moveSpeed = 5f;

    public void Initialize(float speed)
    {
        moveSpeed = Mathf.Max(0.5f, speed);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) ||
            Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }

        if (Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(KeyCode.W) ||
            Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        input = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition =
            body.position +
            input * moveSpeed * Time.fixedDeltaTime;

        body.MovePosition(nextPosition);
    }
}
