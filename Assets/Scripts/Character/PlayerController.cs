using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称角色移动脚本
/// </summary>
[RequireComponent(typeof(CharacterController))]//如果挂载脚本的对象身上没有characterController脚本，则自动添加一个，如果别人试图移除，UNity会拦截， 
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform cameraTransform;//摄像机位置信息
    [SerializeField]
    private InputActionReference moveAction;//InputSystem中的玩家移动变量
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference dodgeAction;
    //[SerializeField]
    //private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float runMultiplier = 1.55f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Dodge")]
    [SerializeField] private float dodgeSpeed = 9f;
    [SerializeField] private float dodgeDuration = 0.35f;
    [SerializeField] private float dodgeCooldown = 0.7f;
    public bool IsGrounded => characterController.isGrounded;
    public bool IsRunning { get; private set; }
    public bool IsDodging { get; private set; }
    public float NormalizedMoveSpeed { get; private set; }

    public event Action JumpStarted;
    public event Action DodgeStarted;

    private CharacterController characterController;
    private float verticalVelocity;
    private Vector3 dodgeDirection;
    private float dodgeEndTime;
    private float nextDodgeTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        sprintAction.action.Enable();
        jumpAction.action.Enable();
        dodgeAction.action.Enable();
        jumpAction.action.performed += OnJump;
        dodgeAction.action.performed += OnDodge;
    }

    private void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        dodgeAction.action.performed -= OnDodge;
        moveAction.action.Disable();
        sprintAction.action.Disable();
        jumpAction.action.Disable();
        dodgeAction.action.Disable();
    }

    private void Update()
    {
        if (GameBootstrap.InputMode != null && !GameBootstrap.InputMode.IsGameplay())
        {
            NormalizedMoveSpeed = 0f;
            return;
        }

        UpdateGravity();

        if (IsDodging)
        {
            UpdateDodge();
        }
        else
        {
            Move();
        }
    }

    private void UpdateGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void Move()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 moveDirection = GetCameraRelativeDirection(input);
        IsRunning = sprintAction.action.IsPressed() && moveDirection.sqrMagnitude > 0.01f;

        float speed = walkSpeed * (IsRunning ? runMultiplier : 1f);
        NormalizedMoveSpeed = moveDirection.sqrMagnitude > 0.01f
            ? (IsRunning ? 1f : 0.5f)
            : 0f;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                rotationSpeed * Time.deltaTime);
        }

        Vector3 finalMove = moveDirection * speed;
        finalMove.y = verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (!CanHandleGameplayInput() || !characterController.isGrounded || IsDodging)
        {
            return;
        }

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        JumpStarted?.Invoke();
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        if (!CanHandleGameplayInput() || IsDodging || Time.time < nextDodgeTime)
        {
            return;
        }

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        dodgeDirection = GetCameraRelativeDirection(input);

        // 没有移动输入时，向角色当前面朝方向闪避。
        if (dodgeDirection.sqrMagnitude < 0.01f)
        {
            dodgeDirection = transform.forward;
        }

        IsDodging = true;
        dodgeEndTime = Time.time + dodgeDuration;
        nextDodgeTime = Time.time + dodgeCooldown;
        NormalizedMoveSpeed = 0f;
        DodgeStarted?.Invoke();
    }

    private void UpdateDodge()
    {
        if (Time.time >= dodgeEndTime)
        {
            IsDodging = false;
            return;
        }

        Vector3 finalMove = dodgeDirection * dodgeSpeed;
        finalMove.y = verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }

    private bool CanHandleGameplayInput()
    {
        return GameBootstrap.InputMode == null || GameBootstrap.InputMode.IsGameplay();
    }

    // PartyController 在切换角色时会调用此方法。
    public void SetMoveSpeed(float value)
    {
        walkSpeed = Mathf.Max(0f, value);
    }
}

