using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcQuestGiver : MonoBehaviour, IInteractable
{
    [SerializeField] private string npcName = "营地向导";
    [TextArea][SerializeField] private string introduction = "荒野很危险。先建立一处篝火营地吧。";
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private UIManager uiManager;

    public string NpcName => npcName;
    public string Introduction => introduction;
    public QuestDefinition Quest => quest;

    public string GetPromptText()
    {
        IQuestService service = QuestServiceContext.Current;
        QuestRuntime runtime = service != null
            ? service.GetQuest(quest.questId)
            : null;

        if (runtime == null)
        {
            return $"按 E 与 {npcName} 对话";
        }

        if (runtime.Status == QuestStatus.Completed)
        {
            return $"按 E 向 {npcName} 交付任务";
        }

        return $"按 E 与 {npcName} 对话";
    }

    public bool TryInteract(GameObject interactor)
    {
        uiManager.OpenNpcDialogue(this);
        return true;
    }
}