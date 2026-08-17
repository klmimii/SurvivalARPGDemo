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

    private void Awake()
    {
        //Awake阶段先默认隐藏背包
        inventoryView.Hide();
        inventoryView.Closed += CloseInventory;

        craftingView.gameObject.SetActive(false);
        //因为合成使用按钮关闭的，所以给他加关闭事件
        craftingView.Closed += CloseCrafting;

        questView.gameObject.SetActive(false);
        questView.Closed += CloseQuest;

        npcDialogueView.gameObject.SetActive(false);
        npcDialogueView.Closed += CloseNpcDialogue;

        // 初始化时隐藏设置面板
        if (settingsPanel != null)
        {
            settingsPanel.gameObject.SetActive(false);
        }

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.AddListener(CloseSettings);
        }
    }

    private void OnDestroy()
    {
        if (inventoryView != null)
        {
            inventoryView.Closed -= CloseInventory;
        }

        if (craftingView!=null)
        {
            craftingView.Closed -= CloseCrafting;
        }

        npcDialogueView.Closed -= CloseNpcDialogue;
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
        // UI 模式中按 B 只关闭背包；若打开的是制作页，不让 B 意外切到背包。
        if (!isInventoryOpen && craftingView.gameObject.activeInHierarchy)
        {
            return;
        }

        //如果背包打开再按B，就关闭背包
        if (isInventoryOpen)
        {
            CloseInventory();
        }
        //如果背包关闭状态，那就打开背包
        else
        {
            OpenInventory();
        }
    }

    /// <summary>
    /// 打开背包方法
    /// </summary>
    public void OpenInventory()
    {
        //如果合成界面是激活状态则直接返回
        if (craftingView.gameObject.activeInHierarchy||questView.gameObject.activeInHierarchy||npcDialogueView.gameObject.activeInHierarchy)
        {
            return;
        }
        //将背包打开状态设为true
        isInventoryOpen = true;
        //打开背包视图
        inventoryView.Show();
        //刷新背包视图
        inventoryPresenter.Open();
        //将输入模式设为UI模式
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
        if(isInventoryOpen||questView.gameObject.activeInHierarchy||npcDialogueView.gameObject.activeInHierarchy)
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
        //如果背包和合成界面都没有打开，则设置模式为gameplay
        if (!isInventoryOpen&& !craftingView.gameObject.activeInHierarchy&& !questView.gameObject.activeInHierarchy && !npcDialogueView.gameObject.activeInHierarchy! && !settingsPanel.gameObject.activeInHierarchy)
        {
            GameBootstrap.InputMode.SetMode(GameInputMode.Gameplay);
        }
    }

    private void OnToggleQuest(InputAction.CallbackContext context)
    {
        if (questView.gameObject.activeInHierarchy)
        {
            CloseQuest();
        }
        else
        {
            OpenQuest();
        }
    }

    public void OpenQuest()
    {
        if (isInventoryOpen || craftingView.gameObject.activeInHierarchy||npcDialogueView.gameObject.activeInHierarchy)
        {
            return;
        }

        questPresenter.Open();
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseQuest()
    {
        if (questView.gameObject.activeInHierarchy)
        {
            questView.gameObject.SetActive(false);
        }

        RestoreGameplayModeIfNoPageOpen();
    }

    public void OpenNpcDialogue(NpcQuestGiver npc)
    {
        if (isInventoryOpen || craftingView.gameObject.activeInHierarchy || questView.gameObject.activeInHierarchy)
        {
            return;
        }

        npcDialoguePresenter.Open(npc);
        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseNpcDialogue()
    {
        if (npcDialogueView.gameObject.activeInHierarchy)
        {
            npcDialogueView.gameObject.SetActive(false);
        }

        RestoreGameplayModeIfNoPageOpen();
    }

    private void OnToggleSettings(InputAction.CallbackContext context)
    {
        if (settingsPanel.gameObject.activeInHierarchy)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        // 互斥判断：如果其他面板处于打开状态，不允许打开设置页
        if (isInventoryOpen || craftingView.gameObject.activeInHierarchy || questView.gameObject.activeInHierarchy || npcDialogueView.gameObject.activeInHierarchy)
        {
            return;
        }

        settingsPanel.gameObject.SetActive(true);

        GameBootstrap.InputMode.SetMode(GameInputMode.UI);
    }

    public void CloseSettings()
    {
        if(settingsPanel.gameObject.activeInHierarchy)
        {
            settingsPanel.gameObject.SetActive(false);
        }

        
        RestoreGameplayModeIfNoPageOpen();
    }
}
