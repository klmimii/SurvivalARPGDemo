using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//ScriptableObject时物品说明书，不是玩家背包。玩家有多少怪物素材，应写在InventoryModel里
/// <summary>
/// （配置层）物品数据配置脚本，静态数据，全游戏只有一份，不管玩家见到了100个苹果，内存里只存在一个Item_Apple.Asset
/// </summary>
//特性用于右键菜单快捷生成，在Unity的Project窗口空白处点击右键，菜单里会出现Create->Servival ARPG->Item Definition
//点击后会自动生成一个名为Item_的asset文件
[CreateAssetMenu(menuName = "Survival ARPG/Item Definition", fileName = "Item_")]//特性创建资源菜单方便策划在编辑器里创建数据
public class ItemDefinition : ScriptableObject
{
    [Tooltip("稳定的唯一 ID：未来存档和网络都依赖他，不要发布后随意修改")]//悬停提示
    //需要itemId是因为虽然每个文件都有文件名，但做游戏存档或网络传输时，不能依赖ScriptableObejct对象的引用（因为存盘文本文件如json/xml挤不下c#的内存指针）
    //存档机制：存档时，背包系统只需要存一个字符串，和数量，读档时根据health_potion这个ID从数据库或Addressables中把对应的ItemDefinition找出来即可
    public string itemId;

    //物品在游戏内显示的中文名（如”红药水“）
    public string displayName;

    [TextArea] //把Inspector面板里的描述文本框变成多行输入框，方便输入大段的物品剧情描述或装备属性说明
    public string description;

    //物品在背包UI里显示的2D图标
    public Sprite icon;

    public ItemCategory category;

    public int sortOrder;

    [Min(1)] //特性，强制限制Inspector面板填写的最小值。防止有人手滑把最大堆叠数填成了0或负数导致背包逻辑死循环
    // 如果是不可堆叠的装备，在Inspector里把maxStack设为1，如果是可堆叠的消耗品/材料，设为99或999
    public int maxStack = 99;

    public ItemType itemType;

    [Header("Only for Consumable")]
    [Min(0)] //限制不能小于0，
    public int healAmount;//消耗品数量，比如血瓶
}
