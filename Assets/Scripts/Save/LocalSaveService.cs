using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LocalSaveService : MonoBehaviour
{
    [SerializeField]
    private Transform playerTransform;//玩家位置信息
    [SerializeField]
    private Health playerHealth;//玩家血量
    [SerializeField]
    private ItemDatabase itemDatabase;//物品数据
    [SerializeField]
    private BuildingDatabase buildingDatabase;//建筑物数据

    //Application.persistentDataPath是Unity提供的标准跨平台持久化目录
    private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    //Update时按下F5触发保存数据，F9触发加载游戏
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
        }

        if(Input.GetKeyDown(KeyCode.F9))
        {
            LoadGame();
        }
    }

    private void SaveGame()
    {
        //实例化总数居包并录入问价数据
        SaveGameData data = new SaveGameData { playerPosition = playerTransform.position, playerRotation = playerTransform.eulerAngles, playerHealth = playerHealth.CurrentHealth };

        //分别采集背包、世界物体状态、建筑物
        CaptureInventory(data);
        CaptureWorldStates(data);
        CaptureBuildings(data);
        CaptureQuests(data);

        //Json工具自动扫描SaveGameData以及他里面包含的所有子类，只要上面包含了[Serializable]就翻译成Json
        //第二个参数控制是否进行美化排版
        string json = JsonUtility.ToJson(data, true);
        //然后保存到之前定义的SavePath中
        File.WriteAllText(SavePath, json);
        Debug.Log($"存档完成：{SavePath}");

    }

    public void LoadGame()
    {
        //检查有没有存档文件
        if (!File.Exists(SavePath))
        {
            Debug.Log("没有找到存档文件");
            return;
        }

        //将文件读入硬盘
        string json = File.ReadAllText(SavePath);
        //反序列化保存到data中
        SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);

        //如果数据为空或者版本不为1，则拦截警报
        if (data == null || data.saveVersion != 1)
        {
            Debug.LogError("存档格式无效或版本不兼容");
            return;
        }

        RestoreInventory(data);//根据data恢复玩家背包中的物品和数量
        RestorePlayer(data);//暂时关闭CharacterController，并把玩家传送到存档坐标
        RestoreWorldStates(data);//按照SaveId找到场景中的树木，宝箱，恢复器显示/隐藏状态
        StartCoroutine(RestoreBuildingsRoutine(data));//先Destroy销毁当前地图的所有建筑，再根据存档Instantiate
        RestoreQuests(data);

        Debug.Log("读档完成");
    }

    private void CaptureInventory(SaveGameData data)
    {
        //遍历背包的每个格子
        foreach(ItemStack slot in GameBootstrap.InventoryModel.Slots)
        {
            //创建极简的存盘数据结构并添加进列表
            data.inventorySlots.Add(new InventorySlotSaveData
            {
                //如果格子是空的，ID存空字符串，否则村物品定义的itemId
                itemId = slot.IsEmpty ? string.Empty : slot.Definition.itemId,
                //如果格子是空的数量存0，否则村实际数量
                amount = slot.IsEmpty ? 0 : slot.Amount
            });
        }
    }

    private void RestoreInventory(SaveGameData data)
    {
        InventoryModel inventory = GameBootstrap.InventoryModel;

        //按照当前背包的最大容量进行循环还原
        for(int i=0;i<inventory.Capacity;i++)
        {
            //如果存档里的格子数量比当前背包少
            if(i>=data.inventorySlots.Count)
            {
                inventory.SetSlot(i, null, 0);//补空槽位
                continue;
            }

            //获取该格子的存档数据
            InventorySlotSaveData saveSlot = data.inventorySlots[i];
            //利用ID去数据库查找真实的配置资产
            ItemDefinition definition = itemDatabase.GetById(saveSlot.itemId);
            //将物品与数量写入背包对应格子
            inventory.SetSlot(i, definition, saveSlot.amount);
        }
        //广播背包已刷新，通知UI重绘
        inventory.NotifyChanged();
    }

    private void RestorePlayer(SaveGameData data)
    {
        //获取玩家身上的CharacterController
        CharacterController controller = playerTransform.GetComponent<CharacterController>();

        //在传送前强行禁用CharacterController，不禁用的话CharacterController会认为自己设置的位置是非法位移，会在下一帧强行把角色修正，导致传送失败
        if(controller!=null)
        {
            controller.enabled = false;
        }

        //一口气设置玩家的位置和旋转角度
        playerTransform.SetPositionAndRotation(data.playerPosition, Quaternion.Euler(data.playerRotation));
        
        //传送完毕后，重新启用
        if(controller!=null)
        {
            controller.enabled = true;
        }

        playerHealth.RestoreHealth(data.playerHealth);
    }

    private void CaptureWorldStates(SaveGameData data)
    {
        //扫描场景中所有MonoBehaviour组件（包括激活是活的对象）
        foreach(MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>(true))
        {
            //模式匹配判断：这个 组件是否实现了ISaveable接口，且SaveId是否有效
            if(behaviour is ISaveable saveable && !string.IsNullOrEmpty(saveable.SaveId))
            {
                //满足条件，抓取器状态并加入存档列表
                data.worldStates.Add(saveable.CaptureState());
            }
        }
    }

    private void RestoreWorldStates(SaveGameData data)
    {
        //步骤1构建ID-状态数据的字典
        Dictionary<string, SaveableData> statesById = new Dictionary<string, SaveableData>();
        foreach(SaveableData state in data.worldStates)
        {
            statesById[state.id] = state;
        }
        //遍历场景中所有的Monobehaviour组件
        foreach(MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>(true))
        {
            //多重条件校验与高效匹配
            if(behaviour is ISaveable saveable && statesById.TryGetValue(saveable.SaveId,out SaveableData state))
            {
                //调用RestoreState恢复物体的显隐，物体碰撞或开启状态
                saveable.RestoreState(state);
            }
        }
    }

    private void CaptureBuildings(SaveGameData data)
    {
        foreach (PlacedBuilding building in
                 FindObjectsOfType<PlacedBuilding>())
        {
            if (building.Definition == null ||
                !building.IsPlayerBuilt)
            {
                continue;
            }

            data.buildings.Add(new BuildingSaveData
            {
                instanceId = building.InstanceId,
                buildingId = building.Definition.buildingId,
                position = building.transform.position,
                rotation = building.transform.eulerAngles,
                paymentSource = building.PaymentSource
            });
        }
    }

    //private void CaptureBuildings(SaveGameData data)
    //{
    //    //遍历场景中所有挂载了PlacedBuilding脚本的物体
    //    foreach(PlacedBuilding building in FindObjectsOfType<PlacedBuilding>())
    //    {
    //        //如果Dinitionweikong ,跳过
    //        if(building.Definition==null)
    //        {
    //            continue;
    //        }
    //        //将实体的Transform空间信息和资产ID提取为极简数据包
    //        data.buildings.Add(new BuildingSaveData { buildingId = building.Definition.buildingId, position = building.transform.position, rotation = building.transform.eulerAngles });
    //    }
    //}

    private IEnumerator RestoreBuildingsRoutine(SaveGameData data)
    {
        foreach (PlacedBuilding building in
                 FindObjectsOfType<PlacedBuilding>())
        {
            if (building.IsPlayerBuilt)
            {
                Destroy(building.gameObject);
            }
        }

        // Destroy在帧末真正生效，等一帧再生成，避免旧Socket残留。
        yield return null;

        foreach (BuildingSaveData savedBuilding in data.buildings)
        {
            BuildingDefinition definition =
                buildingDatabase.GetById(savedBuilding.buildingId);

            if (definition == null || definition.buildingPrefab == null)
            {
                Debug.LogWarning(
                    $"找不到建筑定义：{savedBuilding.buildingId}");
                continue;
            }

            GameObject instance = Instantiate(
                definition.buildingPrefab,
                savedBuilding.position,
                Quaternion.Euler(savedBuilding.rotation));

            PlacedBuilding placedBuilding =
                instance.GetComponent<PlacedBuilding>();

            if (placedBuilding == null)
            {
                Debug.LogError(
                    $"建筑Prefab缺少PlacedBuilding：{definition.name}",
                    definition.buildingPrefab);
                Destroy(instance);
                continue;
            }

            placedBuilding.Initialize(
                definition,
                savedBuilding.instanceId,
                true,
                savedBuilding.paymentSource);
        }

        // 等所有新Prefab的OnEnable和Socket注册完毕。
        yield return null;
        BuildingSocket.RebuildReservations();

        Debug.Log("建筑恢复和吸附点重建完成。");
    }

    //private void RestoreBuildings(SaveGameData data)
    //{
    //    //先清除当前运行时建造物，避免每次读档重复生产
    //    foreach(PlacedBuilding building in FindObjectsOfType<PlacedBuilding>())
    //    {
    //        Destroy(building.gameObject);

    //    }

    //    foreach(BuildingSaveData saveBuilding in data.buildings)
    //    {
    //        //通过ID查询具体的数据资产，
    //        BuildingDefinition definition = buildingDatabase.GetById(saveBuilding.buildingId);
    //        if(definition==null||definition.buildingPrefab==null)
    //        {
    //            Debug.LogWarning($"找不到建筑定义：{saveBuilding.buildingId}");
    //            continue;
    //        }

    //        Instantiate(definition.buildingPrefab, saveBuilding.position, Quaternion.Euler(saveBuilding.rotation));
    //    }
    //}

    /// <summary>
    /// 抓取任务
    /// </summary>
    /// <param name="data"></param>
    private void CaptureQuests(SaveGameData data)
    {
        //遍历所有任务
        foreach (QuestRuntime quest in GameBootstrap.QuestService.Quests)
        {
            data.quests.Add(new QuestSaveData{questId = quest.Definition.questId,status = quest.Status,progress = (int[])quest.Progress.Clone()});
        }
    }

    private void RestoreQuests(SaveGameData data)
    {
        foreach (QuestSaveData savedQuest in data.quests)
        {
            GameBootstrap.QuestService.RestoreQuest(savedQuest.questId,savedQuest.status, savedQuest.progress);
        }
    }
}
