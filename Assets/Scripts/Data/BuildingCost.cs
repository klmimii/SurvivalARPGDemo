using System;
using UnityEngine;

[Serializable]
public class BuildingCost
{
    public ItemDefinition item;

    [Min(1)]
    public int amount = 1;
}