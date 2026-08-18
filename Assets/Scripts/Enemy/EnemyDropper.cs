using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDropper : MonoBehaviour
{
    [SerializeField]
    private PickupItem pickupPrefab;//3D 掉落物预制体
    [SerializeField]
    private ItemDefinition dropItem;//具体掉落什么物品的数据资产
    [SerializeField]
    private int dropAmount = 1;

    private void OnEnable()
    {
        
    }

    public void Drop()
    {
        if (pickupPrefab == null||dropItem==null)
        {
            //第二个参数，控制台点击这个警告，直接定位到传入的GameObject
            Debug.LogWarning("EnemyDropper缺少掉落Prefab或ItemDefinition", this);
            return;
        }

        //掉落位置用当前物体位置加上0.4f的高度偏移，不然生成的掉落物可能因为穿模而卡进地下或地形缝隙中
        Vector3 dropPosition = this.transform.position + Vector3.up * 0.4f;
        //在计算的dropPosition和没有旋转角度的位置创建pickupPrefab
        PickupItem pickup = GameBootstrap.Pool.Spawn(pickupPrefab, dropPosition, Quaternion.identity);
        //动态传入掉落物
        pickup.Setup(dropItem, dropAmount);
    }
}
