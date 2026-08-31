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


    private UIPanelTween panelTween;
    public bool IsVisible => panelTween != null
        ? panelTween.IsVisible
        : panelRoot != null && panelRoot.activeInHierarchy;
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
        SetVisible(false, true);
    }

    public void SetVisible(bool visible)
    {
        SetVisible(visible, false);
    }

    private void SetVisible(bool visible, bool immediate)
    {
        if (panelRoot == null)
        {
            return;
        }

        panelTween = UIPanelTween.GetOrAdd(panelRoot);
        if (visible)
        {
            panelTween.Show();
        }
        else if (immediate)
        {
            panelTween.HideImmediate();
        }
        else
        {
            panelTween.Hide();
        }
    }
}