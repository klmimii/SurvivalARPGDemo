using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]//这个特性能让纯C#类也能直接嵌套再其他的ScriptableObject或MonoBehaviour中，并在UnityInspaector中可视化编辑，允许该数据结构被转换为字节流或json
//配方所需的原料
public class RecipeIngredient 
{
    public ItemDefinition item;//指定需要哪一种原料 
    [Min(1)]
    public int amount = 1;//原料的数量，必须大于等于一
}
