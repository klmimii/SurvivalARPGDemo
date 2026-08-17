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
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private BuildMenuEntry[] entries;
    [SerializeField] private Button demolishButton;
    [SerializeField] private Button closeButton;

    public event Action<BuildingDefinition> DefinitionSelected;
    public event Action DemolishSelected;
    public event Action CloseSelected;

    private void Awake()
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

        demolishButton.onClick.AddListener(
            () => DemolishSelected?.Invoke());
        closeButton.onClick.AddListener(
            () => CloseSelected?.Invoke());
    }

    public void SetVisible(bool visible)
    {
        panelRoot.SetActive(visible);
    }
}