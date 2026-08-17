using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可采集资源节点（采矿点，采药点，树木等）
/// </summary>
public class GatherableNode : MonoBehaviour, IInteractable, ISaveable
{
    [Header("Reward")]
    [SerializeField]
    private ItemDefinition rewardItem;//采集什么物品（如：铁矿）
    [SerializeField]
    private int rewardAmount = 2;//一次给多少个

    [Header("Refresh")]
    [SerializeField]
    private float refreshSeconds = 20f;//采集后需要多少秒刷新
    [SerializeField]
    private GameObject visualRoot;//矿石/草药的3D模型渲染节点
    [SerializeField]
    private Collider interactionCollider;//触发 采集的物理碰撞体

    private bool isAvailable = true;//当前节点是否处于可采集状态

    [SerializeField]
    private string saveId;

    public string SaveId => saveId;

    /// <summary>
    /// 存档
    /// </summary>
    /// <returns></returns>
    public SaveableData CaptureState()
    {
        return new SaveableData
        {
            id = SaveId,
            boolValue=isAvailable
        };

    }

    public string GetPromptText()
    {
        if(!isAvailable)
        {
            return "资源正在恢复";
        }

        return $"按E采集{rewardItem.displayName}x{rewardAmount}";
    }

    /// <summary>
    /// 读档
    /// </summary>
    /// <param name="data"></param>
    public void RestoreState(SaveableData data)
    {
        if(data==null||data.id!=SaveId)
        {
            return;//数据为空或者Id匹配不上，直接忽略
        }
        //恢复数据
        isAvailable = data.boolValue;
        visualRoot.SetActive(isAvailable);//回复视觉 表现
        interactionCollider.enabled = isAvailable;//恢复物理碰撞
    }

    /// <summary>
    /// 尝试采集
    /// </summary>
    /// <param name="interactor"></param>
    /// <returns></returns>
    public bool TryInteract(GameObject interactor)
    {
        //如果资源还在恢复，拒绝响应
        if (!isAvailable)
            return false;

        //尝试塞入背包
        InventoryOperationResult result = GameBootstrap.InventoryService.TryAddItem(rewardItem, rewardAmount);

        //拦截，背包空间不足，采集失败
        if (!result.Success)
            return false;


        //塞包成功，开启隐藏与刷新协程
        StartCoroutine(CollectAndRefresh());
        return true;
    }

    private IEnumerator CollectAndRefresh()
    {
        //1.标记为不可用，并把3D模型隐藏，把碰撞体关闭（玩家既看不见也摸不到）
        isAvailable = false;
        visualRoot.SetActive(false);
        interactionCollider.enabled = false;

        //第二步：协程暂停等待20秒（可优化，用系统时间戳记录（Time.time+20f））
        yield return new WaitForSeconds(refreshSeconds);

        isAvailable = true;
        visualRoot.SetActive(true);
        interactionCollider.enabled = true;
    }

}
