using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartyPresenter : MonoBehaviour
{
    [SerializeField] private PartyController partyController;//控制角色脚本，知道当前谁在场上，以及队伍里有谁
    [SerializeField] private PartyHudView partyHudView;//纯UI脚本，负责屏幕角落的角色头像等

    //private void Start()
    //{
    //    //订阅事件
    //    partyController.ActiveMemberChanged += Refresh;
    //    //主动刷新一次UI
    //    Refresh(partyController.ActiveMember);
    //}

    //private void OnDestroy()
    //{
    //    if (partyController != null)
    //    {
    //        partyController.ActiveMemberChanged -= Refresh;
    //    }
    //}

    private void Start()
    {
        if (partyController != null)
        {
            Bind(partyController);
        }
    }

    private void OnDestroy()
    {
        if (partyController != null)
        {
            partyController.ActiveMemberChanged -= Refresh;
        }
    }

    public void Bind(PartyController value)
    {
        if (partyController != null)
        {
            partyController.ActiveMemberChanged -= Refresh;
        }

        partyController = value;

        if (partyController == null)
        {
            partyHudView.Render(null);
            return;
        }

        partyController.ActiveMemberChanged -= Refresh;
        partyController.ActiveMemberChanged += Refresh;
        Refresh(partyController.ActiveMember);
    }

    private void Refresh(CharacterDefinition definition)
    {
        //交给UI来渲染
        partyHudView.Render(definition);
    }
}
