using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingDatabase : MonoBehaviour
{
    [SerializeField]
    private BuildingDefinition[] buildings;//在面板上吧所有建筑配置文件拖到这个数组中
    private Dictionary<string, BuildingDefinition> buildingById;//运行时用到的内存索引字典

    private void Awake()
    {
        buildingById = new Dictionary<string, BuildingDefinition>();
        foreach(BuildingDefinition building in buildings)
        {
            //确保配置不为空且ID有效
            if(building!=null&&!string.IsNullOrEmpty(building.buildingId))
            {
                //存入字典
                buildingById[building.buildingId] = building;
            }
        }
    }

    public BuildingDefinition GetById(string buildingId)
    {
        //如果Key不存在，TryGetValue会把definition置为null并返回false
        buildingById.TryGetValue(buildingId, out BuildingDefinition definition);
        return definition;
    }
}
