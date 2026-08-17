using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家交互检测控制器
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField]
    private InputActionReference interactAction;//交互按键
    [SerializeField]
    private float interactRadius = 2f;//交互半径
    [SerializeField]
    private LayerMask interactableLayers;//可交互的层级
    [SerializeField]
    private InteractionPrompt prompt;//交互文字
    [SerializeField]
    private ToastView toastView;

    //存储当前的可交互物
    private IInteractable currentInteractable;

    private void OnEnable()
    {
        interactAction.action.Enable();
        interactAction.action.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        interactAction.action.Disable();
        interactAction.action.performed -= OnInteractPerformed;
    }

    /// <summary>
    /// 每帧调用 FindNearestInteractablr寻找周围最近的可交互物
    /// </summary>
    private void Update()
    {
        //如果输入模式不是空并且不是游戏模式，则让当前玩家准心对着的可交互物清空，确保后台不会判断玩家还一直对着某个可交互物
        if(GameBootstrap.InputMode!=null&& !GameBootstrap.InputMode.IsGameplay())
        {
            currentInteractable = null;
            //关闭屏幕上的交互提示文字
            prompt.Hide();
            //不再执行后面的采集逻辑
            return;
        }

        currentInteractable = FindNearestInteractable();

        if(currentInteractable==null)
        {
            prompt.Hide();
            return;
        }

        prompt.Show(currentInteractable.GetPromptText());
  
    }

    /// <summary>
    /// 当玩家按下交互键时，直接将当前的玩家物体gameObject传给TryInteract接口执行真正的逻辑
    /// </summary>
    /// <param name="context"></param>
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        //如果不是游戏模式，则不执行按E键采集的逻辑
        if(GameBootstrap.InputMode!=null&&!GameBootstrap.InputMode.IsGameplay())
        {
            return;
        }
        //如果周围没有可交互物，隐藏悬浮提示框
        if (currentInteractable == null)
        {
            return;
        }

        //虽然变量声明的类型时IInteractable接口，但是在运行时，这个变量在内存中指向的永远是真正的游戏对象实例
        //比如靠近地上的苹果，FindNearestInteractable再物理检测中拿到了苹果上的PickupItem并返回，那么currentInteractable指向的就时PickUpItem
        bool success=currentInteractable.TryInteract(gameObject);
        if(!success)
        {
            toastView.Show("交互失败，请检查背包空间或条件");
        }
    }

    /// <summary>
    /// 找到最近的可交互物
    /// </summary>
    /// <returns></returns>
    private  IInteractable FindNearestInteractable()
    {
        //球形重叠检测,以玩家为中心，interactRadius为半径画一个隐形的球体 ，一次性找出这个范围内的所有碰撞体
        Collider[] colliders = Physics.OverlapSphere(this.transform.position, interactRadius, interactableLayers);

        IInteractable nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        //遍历检测出来的碰撞器
        foreach(Collider targetCollider in colliders)
        {
            IInteractable interactable = targetCollider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                continue;
            }

            float distanceSqr = (targetCollider.transform.position - this.transform.position).sqrMagnitude;
            if(distanceSqr<nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = interactable;
            }
        }

        return nearest;

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.transform.position, interactRadius);
    }
}
