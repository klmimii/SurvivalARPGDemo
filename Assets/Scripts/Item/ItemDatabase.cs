using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    [SerializeField]
    private ItemDefinition[] items;//在编辑器中国配置的物品列表数组
    [SerializeField]
    private Dictionary<string, ItemDefinition> itemById;//物品的唯一ID，对应的物品配置对象，用数组或列表查找物品，算法复杂度是O（n）而字典是O（1）

    private void Awake()
    {
        itemById = new Dictionary<string, ItemDefinition>();

        foreach(ItemDefinition item in items)
        {
            //过滤空物品或ID没填的非法配置,不仅检查空，还拦截了空格或空字符串
            if (item==null||string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }
            //如果有重复的物品ID，拦截
            if(itemById.ContainsKey(item.itemId))
            {
                Debug.LogError($"重复物品 ID：{item.itemId}", this);
                continue;
            }

            //上面都没有问题就将他加入字典
            itemById.Add(item.itemId, item);
        }
    }

    public ItemDefinition GetById(string itemId)
    {
        //如果传入的要查找的是空或空串，直接返回null
        if(string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        //2.通过字典的查找方法查找传入的物品，找到就返回item，找不到就返回null
        itemById.TryGetValue(itemId, out ItemDefinition item);
        return item;
    }
}
