using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private InputActionReference toggleInventotyAction;//绑定配置好的开关动作
    [SerializeField]
    private InventoryView inventoryView;//界面控制脚本
    [SerializeField]
    private InventoryPresenter inventoryPresenter;//数据中介脚本

    [SerializeField] private CraftingView craftingView; // 合成台脚本
    [SerializeField] private CraftingPresenter craftingPresenter;//合成台数据中介脚本

    private bool isInventoryOpen;//记录背包当前是开着的还是关着的

    [SerializeField] private InputActionReference toggleQuestAction;
    [SerializeField] private QuestView questView;
    [SerializeField] private QuestPresenter questPresenter;

    [SerializeField] private NpcDialogueView npcDialogueView;
    [SerializeField] private NpcDialoguePresenter npcDialoguePresenter;

    [SerializeField] private InputActionReference toggleSettingsAction; // 绑定的 Esc 快捷键动作
    [SerializeField] private AudioSettingsPresenter settingsPanel;                  // SettingsPanel 物体

    [SerializeField] private Button settingsCloseButton;
    private UIPanelTween settingsTween;
    private void Awake()
    {
        inventoryView.HideImmediate();
        inventoryView.Closed += CloseInventory;

        UIPanelTween.GetOrAdd(craftingView.gameObject).HideImmediate();
        craftingView.Closed += CloseCrafting;

        UIPanelTween.GetOrAdd(questView.gameObject).HideImmediate();
        questView.Closed += CloseQuest;

        UIPanelTween.GetOrAdd(npcDialogueView.gameObject).HideImmediate();
        npcDialogueView.Closed += CloseNpcDialogue;

        if (settingsPanel != null)
        {
            settingsTween = UIPanelTween.GetOrAdd(settingsPanel.gameObject);
            settingsTween.HideImmediate();
        }

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.AddListener(CloseSettings);
        }
    }

    private void OnDestroy()
    {
        if (inventoryView != null) inventoryView.Closed -= CloseInventory;
        if (craftingView != null) craftingView.Closed -= CloseCrafting;
        if (questView != null) questView.Closed -= CloseQuest;
        if (npcDialogueView != null) npcDialogueView.Closed -= CloseNpcDialogue;
        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.RemoveListener(CloseSettings);
        }
    }

    private void OnEnable()
    {
        toggleInventotyAction.action.Enable();
        toggleInventotyAction.action.performed += OnToggleInventory;

        toggleQuestAction.action.Enable();
        toggleQuestAction.action.performed += OnToggleQuest;

        if (toggleSettingsAction != null)
        {
            toggleSettingsAction.action.Enable();
            toggleSettingsAction.action.performed += OnToggleSettings;
        }
    }

    private void OnDisable()
    {
        toggleInventotyAction.action.performed -= OnToggleInventory;
        toggleInventotyAction.action.Disable();

        toggleQuestAction.action.performed -= OnToggleQuest;
        toggleQuestAction.action.Disable();

        if (toggleSettingsAction != null)
        {
            toggleSettingsAction.action.performed -= OnToggleSettings;
            toggleSettingsAction.action.Disable();
        }
    }

    /// <summary>
    /// 按背包按钮做什么
    /// </summary>
    /// <param name="context"></param>
    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (!isInventoryOpen && craftingView.IsVisible) return;

        if (isInventoryOpen) CloseInventory();
        else OpenInventory();
    }

    /// <summary>
    /// 打开背包方法
    /// </summary>
    public void OpenInventory()
    {
        if (craftingView.IsVisible || questView.IsVisible || npcDialogueView.IsVisible)
        {
            return;
        }

        isInventoryOpen = true;
        inventoryView.Show();
        inventoryPresenter.Open();
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    /// <summary>
    /// 关闭背包方法
    /// </summary>
    public void CloseInventory()
    {
        //背包打开状态为false
        isInventoryOpen = false;
        //背包视图隐藏
        inventoryView.Hide();
        //如果没有其他页面打开将输入模式改为gameplay
        RestoreGameplayModeIfNoPageOpen();

    }

    /// <summary>
    /// 打开工作台方法
    /// </summary>
    public void OpenCrafting()
    {
        //如果背包是打开状态，关闭背包
        if (isInventoryOpen || questView.gameObject.activeInHierarchy || npcDialogueView.gameObject.activeInHierarchy)
        {
            CloseInventory();
        }

        //打开背包界面
        craftingPresenter.Open();
        //将输入模式设置为UI
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);

    }

    public void CloseCrafting()
    {
        RestoreGameplayModeIfNoPageOpen();
    }

    private void RestoreGameplayModeIfNoPageOpen()
    {
        bool settingsOpen = settingsTween != null && settingsTween.IsVisible;
        if (!isInventoryOpen &&
            !craftingView.IsVisible &&
            !questView.IsVisible &&
            !npcDialogueView.IsVisible &&
            !settingsOpen)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }
    }

    private void OnToggleQuest(InputAction.CallbackContext context)
    {
        if (questView.IsVisible) CloseQuest();
        else OpenQuest();
    }

    public void OpenQuest()
    {
        if (isInventoryOpen || craftingView.IsVisible || npcDialogueView.IsVisible)
        {
            return;
        }

        questPresenter.Open();
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseQuest()
    {
        if (questView.IsVisible)
        {
            UIPanelTween.GetOrAdd(questView.gameObject).Hide();
        }

        RestoreGameplayModeIfNoPageOpen();
    }

    public void OpenNpcDialogue(NpcQuestGiver npc)
    {
        if (isInventoryOpen || craftingView.IsVisible || questView.IsVisible)
        {
            return;
        }

        npcDialoguePresenter.Open(npc);
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseNpcDialogue()
    {
        if (npcDialogueView.IsVisible)
        {
            UIPanelTween.GetOrAdd(npcDialogueView.gameObject).Hide();
        }

        RestoreGameplayModeIfNoPageOpen();
    }

    private void OnToggleSettings(InputAction.CallbackContext context)
    {
        if (settingsTween != null && settingsTween.IsVisible) CloseSettings();
        else OpenSettings();
    }

    public void OpenSettings()
    {
        if (isInventoryOpen ||
            craftingView.IsVisible ||
            questView.IsVisible ||
            npcDialogueView.IsVisible ||
            settingsPanel == null)
        {
            return;
        }

        settingsTween = UIPanelTween.GetOrAdd(settingsPanel.gameObject);
        settingsTween.Show();
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseSettings()
    {
        if (settingsTween != null && settingsTween.IsVisible)
        {
            settingsTween.Hide();
        }

        RestoreGameplayModeIfNoPageOpen();
    }
}
