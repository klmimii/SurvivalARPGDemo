using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏启动时，她负责把长辈InventoryModel建好，把管理员LocalInventoryService招进来，并把账本交给管理员，最后把这个管理员挂载静态变量上，方便全局访问
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    //学习圆形中使用静态访问，便于减少初始化样板代码
    //正式项目后期可替换为更规范的服务注册/依赖注入方式
    //将背包数据模型和背包服务作为静态只读属性对外暴漏。游戏里的任何UI脚本或玩家拾取脚本，不需要再场景里去GameObject.Find
    //直接通过GameBootstrap.InventoryService.TryAddItem就能安全 快捷的调用服务
    public static InventoryModel InventoryModel { get; private set; }
    //声明背包服务接口，不写成LocalInventoryService,是因为写成Local就强绑定了，
    public static IInventoryService InventoryService { get; private set; }
    //声明合成服务接口

    public static ICraftingService CraftingService { get; private set; }

    public static IBuildingService BuildingService { get; private set; }

    [SerializeField]
    private GameInputModeService inputModeService;
    public static GameInputModeService InputMode { get; private set; }

    public static GameEventHub Events { get; private set; }
    public static IQuestService QuestService { get; private set; }

    //任务定义
    [SerializeField] private QuestDefinition[] initialQuests;

    [SerializeField] private PoolService poolService;
    public static PoolService Pool { get; private set; }

    public static IAssetService AssetService { get; private set; }

    [SerializeField] private CombatOverlayService combatOverlayService;
    public static CombatOverlayService CombatOverlay { get; private set; }

    [SerializeField] private AudioService audioService;
    public static AudioService Audio { get; private set; }

    public static IRecipeCostService RecipeCostService { get; private set; }

    private void Awake()
    {
        //防止场景误放了两个GamebootStrap导致服务被重复创建
        if(InventoryModel!=null)
        {
            Debug.Log("GameBootStrap已经初始化过，重复对象将被销毁");
            Destroy(gameObject);
            return;
        }
        InputMode = inputModeService;
        InputMode.SetMode(GameInputMode.Gameplay);
        //实例化全局唯一的InventoryModel
        InventoryModel = new InventoryModel(12);
        //实例化全局唯一的游戏事件中心
        Events = new GameEventHub();
        //将InventoryModel作为构造函数参数传入LocalInventoryService，现在用的是Local，之后要做网络版直接改这里new Network...就行
        InventoryService = new LocalInventoryService(InventoryModel);
        RecipeCostService = new LocalRecipeCostService(InventoryService);
        //将事件和背包服务作为参数传入事件服务
        QuestService = new LocalQuestService(Events, InventoryService);
        QuestService.Initialize(initialQuests);
        //将背包服务作为构造函数的参数传入合成服务
        CraftingService = new LocalCraftingService(InventoryService,RecipeCostService);
        //将背包服务作为构造函数传入建造服务
        BuildingService = new LocalBuildingService(InventoryService,RecipeCostService);

        Pool = poolService;

        AssetService = new AddressablesAssetService();
        CombatOverlay = combatOverlayService;
        Audio = audioService;
    }

    private void OnDestroy()
    {
        //在编辑器停止Play时清理静态引用，避免下次运行残留
        //因为Unity编辑器中计时点击了停止运行，Unity并不一定会重新加载整个C# AppDomain，这意味着static变量指向的对象可能仍然滞留在内存中
        //如果不手动作null还原，下一次你再次点击播放时，上一次运行生成的静态数据还残留在内存中，导致逻辑状态污染或内存泄漏
        if(InventoryModel!=null)
        {
            BuildingService = null;
            CraftingService = null;
            RecipeCostService = null;
            InventoryService = null;
            InventoryModel = null;

            InputMode = null;
            Events = null;
            QuestService = null;
            Pool = null;
            AssetService = null;
            CombatOverlay = null;
            Audio = null;
        }
    }


}
