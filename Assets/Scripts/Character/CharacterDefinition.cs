using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//角色定义
[CreateAssetMenu(menuName = "Survival ARPG/Character Definition", fileName = "Character_")]
public class CharacterDefinition : ScriptableObject
{
    public string characterId;//角色ID
    public string displayName;//角色名字
    [TextArea] public string description;//角色描述
    public GameObject visualPrefab;//可视的预设体
    [Min(1)] public int maxHealth = 100;//生命值
    [Min(1f)] public float moveSpeed = 5f;//移动速度

    public WeaponDefinition defaultWeapon;
}
