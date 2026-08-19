using System;
using UnityEngine;

[Serializable]
public sealed class RemoteItemPresentationEntry
{
    [Tooltip("必须等于 ItemDefinition.itemId")]
    public string itemId;

    public bool overrideDisplayName;
    public string displayName;

    public bool overrideDescription;
    [TextArea(3, 8)]
    public string description;

    public bool overrideIcon;
    public Sprite icon;
}

[CreateAssetMenu(
    menuName = "Survival ARPG/Remote Item Presentation Config",
    fileName = "RemoteItemPresentationConfig")]
public sealed class RemoteItemPresentationConfig : ScriptableObject
{
    public string contentVersion = "1.0.0";

    [TextArea]
    public string updateNote;

    public RemoteItemPresentationEntry[] entries;
}