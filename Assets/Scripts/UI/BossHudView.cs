using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private Image healthFillImage;

    public void Show(
        string bossName,
        int currentHealth,
        int maxHealth)
    {
        gameObject.SetActive(true);
        bossNameText.text = bossName;
        Refresh(currentHealth, maxHealth);
    }

    public void Refresh(int currentHealth, int maxHealth)
    {
        // 防止maxHealth意外为0时出现除零错误。
        float percent = maxHealth > 0
            ? (float)currentHealth / maxHealth
            : 0f;

        healthFillImage.fillAmount = Mathf.Clamp01(percent);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;
//using UnityEngine.UI;

//public class BossHudView : MonoBehaviour
//{
//    [SerializeField] private TMP_Text bossNameText;//显示boss名字的文本控件
//    [SerializeField] private Slider healthSlider;//显示boss血量的滑动条控件

//    public void Show(string bossName, int currentHealth, int maxHealth)
//    {
//        gameObject.SetActive(true);//激活UI界面
//        bossNameText.text = bossName;//显示boss的名字
//        healthSlider.maxValue = maxHealth;
//        healthSlider.value = currentHealth;
//    }

//    public void Refresh(int currentHealth, int maxHealth)
//    {
//        healthSlider.maxValue = maxHealth;//重新同步最大血量和当前剩余雪玲
//        healthSlider.value = currentHealth;
//    }

//    public void Hide()
//    {
//        gameObject.SetActive(false);
//    }
//}