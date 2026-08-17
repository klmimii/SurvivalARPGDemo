using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 负责提示文本框的显示隐藏以及文字更新
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [SerializeField]
    private TMP_Text promptText;

    //显示并更新文本
    public void Show(string text)
    {
        //显示传入的字符串，比如（按E拾取苹果x1）
        promptText.gameObject.SetActive(true);
        promptText.text = text;
    }

    //当玩家走开或者周围没有任何可交互物体时，直接把promptText.gameObject禁用
    public void Hide()
    {
        promptText.gameObject.SetActive(false);
    }

}
