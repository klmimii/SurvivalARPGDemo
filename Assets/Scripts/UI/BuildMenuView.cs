using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BuildMenuEntry
{
    public Button button;
    public BuildingDefinition definition;
}

public class BuildMenuView : MonoBehaviour
{
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private BuildMenuEntry[] entries;

    [SerializeField]
    private Button demolishButton;

    [SerializeField]
    private Button closeButton;

    public event Action<BuildingDefinition> DefinitionSelected;
    public event Action DemolishSelected;
    public event Action CloseSelected;

    private void Awake()
    {
        if (entries != null)
        {
            foreach (BuildMenuEntry entry in entries)
            {
                if (entry == null || entry.button == null)
                {
                    continue;
                }

                BuildMenuEntry capturedEntry = entry;
                capturedEntry.button.onClick.AddListener(
                    () => DefinitionSelected?.Invoke(
                        capturedEntry.definition));
            }
        }

        if (demolishButton != null)
        {
            demolishButton.onClick.AddListener(
                () => DemolishSelected?.Invoke());
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                () => CloseSelected?.Invoke());
        }

        // 场景一开始就隐藏，不再等待网络玩家生成。
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }
}