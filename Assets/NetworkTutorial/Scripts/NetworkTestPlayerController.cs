using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 只用于网络测试场景的胶囊移动。
/// IsOwner保证每个窗口只能读取并控制自己的玩家对象。
/// </summary>
public sealed class NetworkTestPlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private Renderer bodyRenderer;

    // 颜色由服务器写，所有客户端读取。
    private readonly NetworkVariable<Color> playerColor = new(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        playerColor.OnValueChanged += OnColorChanged;

        if (IsServer)
        {
            // ClientId 0通常是Host，显示蓝色；ClientId 1显示橙色。
            playerColor.Value = OwnerClientId % 2 == 0
                ? new Color(0.2f, 0.55f, 1f)
                : new Color(1f, 0.45f, 0.15f);
        }

        ApplyColor(playerColor.Value);

        if (IsOwner)
        {
            // 防止两个玩家出生时完全重叠。
            transform.position = new Vector3(
                OwnerClientId * 3f - 1.5f,
                1f,
                0f);

            Debug.Log(
                $"本机拥有玩家对象，ClientId={OwnerClientId}",
                this);
        }
        else
        {
            Debug.Log(
                $"这是远端玩家表现，OwnerClientId={OwnerClientId}",
                this);
        }
    }

    public override void OnNetworkDespawn()
    {
        playerColor.OnValueChanged -= OnColorChanged;
    }

    private void Update()
    {
        // 最关键的判断：不是本机拥有的玩家，绝对不读取键盘。
        if (!IsOwner)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 movement = new Vector3(input.x, 0f, input.y);
        transform.position += movement * moveSpeed * Time.deltaTime;

        if (movement.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(movement);
        }
    }

    private void OnColorChanged(Color previous, Color current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(Color color)
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }
    }
}