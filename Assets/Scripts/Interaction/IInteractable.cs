using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 未来采集树，矿石，宝箱，工作台等等可以交互的行为都实现这个接口。玩家不必写一串if(obj is)
/// </summary>
public interface IInteractable 
{
    //动态UI提示文字，返回交互对象要在屏幕上显示的提示文字，比如按E键拾取苹果
    string GetPromptText();
    //将交互发起者GameObject interactor传进去，因为被交互对象往往需要知道谁在点我，
    bool TryInteract(GameObject interactor);
}
