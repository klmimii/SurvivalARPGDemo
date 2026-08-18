using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 只让本机拥有的网络玩家读取交互输入。
/// 它只负责“寻找对象并调用 IInteractable”，
/// 奖励与状态修改仍由具体网络对象交给服务器处理。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerInteraction : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference interactAction;

    [Header("Detection")]
    [Min(0.1f)]
    [SerializeField]
    private float interactRadius = 2f;

    [SerializeField]
    private LayerMask interactableLayers;

    [Min(1)]
    [SerializeField]
    private int colliderBufferSize = 24;

    private Collider[] colliderBuffer;
    private IInteractable currentInteractable;
    private InteractionPrompt prompt;
    private ToastView toastView;
    private bool inputBound;

    private void Awake()
    {
        colliderBuffer = new Collider[Mathf.Max(1, colliderBufferSize)];
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        prompt = Object.FindObjectOfType<InteractionPrompt>(true);
        toastView = Object.FindObjectOfType<ToastView>(true);

        BindInput();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            HidePrompt();
            UnbindInput();
        }
    }

    private void OnDisable()
    {
        HidePrompt();
        UnbindInput();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
        {
            return;
        }

        if (GameBootstrap.InputMode != null &&
            !GameBootstrap.InputMode.IsGameplay())
        {
            currentInteractable = null;
            HidePrompt();
            return;
        }

        currentInteractable = FindNearestInteractable();

        if (currentInteractable == null)
        {
            HidePrompt();
            return;
        }

        if (prompt != null)
        {
            prompt.Show(currentInteractable.GetPromptText());
        }
    }

    private void BindInput()
    {
        if (inputBound || interactAction == null)
        {
            return;
        }

        interactAction.action.Enable();
        interactAction.action.performed += OnInteractPerformed;
        inputBound = true;
    }

    private void UnbindInput()
    {
        if (!inputBound || interactAction == null)
        {
            return;
        }

        interactAction.action.performed -= OnInteractPerformed;
        interactAction.action.Disable();
        inputBound = false;
    }

    private void OnInteractPerformed(
        InputAction.CallbackContext context)
    {
        if (!IsSpawned || !IsOwner || currentInteractable == null)
        {
            return;
        }

        if (GameBootstrap.InputMode != null &&
            !GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }

        bool accepted = currentInteractable.TryInteract(gameObject);

        // accepted=false 只代表请求没有成功提交或本地条件不满足。
        // 服务器最终拒绝的情况，会在以后用专门结果消息完善。
        if (!accepted && toastView != null)
        {
            toastView.Show("交互请求未能提交。");
        }
    }

    private IInteractable FindNearestInteractable()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            interactRadius,
            colliderBuffer,
            interactableLayers,
            QueryTriggerInteraction.Collide);

        IInteractable nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider targetCollider = colliderBuffer[i];
            if (targetCollider == null)
            {
                continue;
            }

            IInteractable interactable =
                targetCollider.GetComponentInParent<IInteractable>();

            if (interactable == null)
            {
                continue;
            }

            float distanceSqr =
                (targetCollider.transform.position - transform.position)
                .sqrMagnitude;

            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = interactable;
            }
        }

        return nearest;
    }

    private void HidePrompt()
    {
        if (prompt != null)
        {
            prompt.Hide();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}