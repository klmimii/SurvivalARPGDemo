using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform target;//摄像机要跟随的目标
    [SerializeField]
    private InputActionReference lookAction;//新版Input System的鼠标/手柄视角输入动作

    [Header("Camera Settings")]
    [SerializeField]
    private Vector3 offset = new Vector3(0f, 2.2f, -4.5f);//相对目标的偏移量，偏上，偏后
    [SerializeField]
    private float positionSmoothTime = 0.08f;//缓冲平滑时间
    [SerializeField]
    private float sensitivity = 0.08f;//鼠标灵敏度
    [SerializeField]
    private float minPicth=-30f;//俯仰角下限 防止穿透地面
    [SerializeField]
    private float maxPitch=60f;//俯仰角上限 防止视角颠倒

    private float yaw;//偏航角
    private float pitch = 15f;//f俯仰角
    private Vector3 velocity;//速度，用于后面SmoothDamp函数接收速度参数

    [Header("Aim Camera")]
    [SerializeField]
    private Vector3 aimOffset = new Vector3(0.65f, 1.9f, -2.8f);

    [SerializeField] private float normalFieldOfView = 60f;
    [SerializeField] private float aimFieldOfView = 50f;
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeFrequency = 28f;

    private float shakeEndTime;
    private float shakeDuration;
    private float shakeStrength;
    private float shakeSeed;

    private Camera cameraComponent;
    private Vector3 currentOffset;
    private bool isAiming;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        currentOffset = offset;

        if (cameraComponent != null)
        {
            cameraComponent.fieldOfView = normalFieldOfView;
        }
        shakeSeed = Random.Range(0f, 1000f);
    }

    //新版Input System的规范，使用InputActionReference时 必须显示调用.Enable()才能读取输入数据，脚本禁用时调用.Disable()防止不必要的性能开销和报错
    //摄像机脚本激活时
    private void OnEnable()
    {
        lookAction.action.Enable();//开启输入监听
        Cursor.lockState = CursorLockMode.Locked;//锁定鼠标到屏幕中央
        Cursor.visible = false;//隐藏鼠标指针

    }

    //摄像机脚本失活时
    private void OnDisable()
    {
        lookAction.action.Disable();//禁用输入监听
        Cursor.lockState = CursorLockMode.None;//释放鼠标
        Cursor.visible = true;//显示鼠标

    }

    //在这里更新是因为在Unity中 角色移动和动画通常在Update()和FixedUpdate中计算，如果摄像机也在Update()阶段更新，可能会出现摄像机先移动，
    //角色后移动的先后顺序紊乱，导致画面剧烈抖动
    //而LateUpdate会保证所有角色的移动逻辑全部算完之后才执行， 这样摄像机拿到的target.position就是角色在这一帧最准确的最终位置，保证视角跟随极其平滑
    private void LateUpdate()
    {
        //如果不是游戏模式，则禁止镜头移动，不执行后面的逻辑
        if(GameBootstrap.InputMode!=null&&!GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }
        if(target==null)
        {
            return;
        }

        //4.视角旋转数学计算（核心逻辑）
        //lookAciton是InoutAction中鼠标视角转动动作，lookAction.action是拿到里面真正的输入动作对象，然后读取这个输入动作当前的值ReadValue
        Vector2 lookDelta = lookAction.action.ReadValue<Vector2>();
        //因为lookAction读取的时当前帧鼠标移动的像素偏移量，帧率是不稳定的，如果不成一Time.deltaTIme，当帧率波动或者鼠标快速晃动时，yaw和pitch的计算数值会产生极大的阶梯式突变
        ////导致时间看起来忽快忽慢，机器剧烈的抖动，乘以deltaTime，确保不同帧率下旋转平滑稳定
        yaw += lookDelta.x * sensitivity*Time.deltaTime;//左右滑动鼠标->修改偏航角（Yaw）
        pitch -= lookDelta.y * sensitivity*Time.deltaTime;//上下滑动鼠标->修改俯仰角（Pitch）
        pitch = Mathf.Clamp(pitch, minPicth, maxPitch);//限制上下角度

        //记录需要改变多少的角度信息
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);


        Vector3 targetOffset = isAiming ? aimOffset : offset;

        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            aimTransitionSpeed * Time.deltaTime);

        if (cameraComponent != null)
        {
            float targetFov = isAiming
                ? aimFieldOfView
                : normalFieldOfView;

            cameraComponent.fieldOfView = Mathf.Lerp(
                cameraComponent.fieldOfView,
                targetFov,
                aimTransitionSpeed * Time.deltaTime);
        }


        //如果只写target.position+offset，摄像机只会死板的跟着角色平移，鼠标旋转时摄像机并不会绕着角色转
        //用旋转四元数rotation去乘以便宜向量offset,相当于把offset这个偏移拉伸向量，绕着角色旋转了对应的角度
        //Vector3 desiredPosition = target.position + rotation * offset;
        Vector3 desiredPosition = target.position + rotation * currentOffset;

        //5.平滑移动-位置跟随
        //SmoothDamp是用来实现弹簧阻尼平滑插值，参数（当前位置，目标位置，速度变量，平滑时间）会慢慢加速靠近目标，快抵达时自动减慢
        //ref是引用传递，加了ref之后函数可以直接操作你外面定义的原始变量，而不是副本，因为SmoothDamp内部需要保持每一帧的速度，需要把计算出来的最新速度写回外部的速度变量。ref传入前必须初始化，可读可写，out传入前可以不用赋值，方法会给他赋值
        //this.transform.position = Vector3.SmoothDamp(this.transform.position, desiredPosition, ref velocity, positionSmoothTime);
        ////6.朝向即时移动
        //this.transform.rotation = rotation;

        //5.平滑移动-位置跟随（先计算出不含震动的平滑目标位置）
        Vector3 smoothedPosition = Vector3.SmoothDamp(this.transform.position, desiredPosition, ref velocity, positionSmoothTime);

        //6.叠加相机震动偏移
        Vector3 shakeOffset = CalculateShakeOffset(rotation);
        this.transform.position = smoothedPosition + shakeOffset;

        //7.朝向即时移动
        this.transform.rotation = rotation;
    }
    public void SetAimMode(bool aiming)
    {
        isAiming = aiming;
    }

    public void Shake(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f)
        {
            return;
        }

        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeEndTime = Mathf.Max(shakeEndTime, Time.time + duration);
        shakeStrength = Mathf.Max(shakeStrength, strength);
    }

    private Vector3 CalculateShakeOffset(Quaternion cameraRotation)
    {
        if (Time.time >= shakeEndTime || shakeDuration <= 0f)
        {
            shakeStrength = 0f;
            return Vector3.zero;
        }

        float remaining = shakeEndTime - Time.time;
        float normalized = Mathf.Clamp01(remaining / shakeDuration);
        float currentStrength = shakeStrength * normalized * normalized;

        float sampleTime = Time.time * shakeFrequency;
        float x = Mathf.PerlinNoise(shakeSeed, sampleTime) * 2f - 1f;
        float y = Mathf.PerlinNoise(shakeSeed + 31.7f, sampleTime) * 2f - 1f;

        Vector3 localOffset = new Vector3(x, y, 0f) * currentStrength;
        return cameraRotation * localOffset;
    }

    /// <summary>
    /// 本机网络玩家生成后重新指定跟随目标。
    /// 传入 null 时停止跟随。
    /// </summary>
    public void SetTarget(Transform value)
    {
        target = value;
        velocity = Vector3.zero;
    }
}
