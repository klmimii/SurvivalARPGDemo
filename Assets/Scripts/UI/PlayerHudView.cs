using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudView : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthFillImage;

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            Debug.LogError(
                "PlayerHudView没有绑定Player Health。",
                this);
            return;
        }

        playerHealth.HealthChanged += RefreshHealth;

        RefreshHealth(
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.HealthChanged -= RefreshHealth;
        }
    }

    private void RefreshHealth(int current, int max)
    {
        healthText.text = $"HP: {current} / {max}";

        float percent = max > 0
            ? (float)current / max
            : 0f;

        healthFillImage.fillAmount = Mathf.Clamp01(percent);
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;

///// <summary>
///// 只负责显示血量，不掺和任何血量计算的逻辑。HUD的意思是Heads-up Display指不跟随世界3D物体固定显示在屏幕画面上的UI界面
///// </summary>
//public class PlayerHudView : MonoBehaviour
//{
//    [SerializeField]
//    private Health playerHealth;
//    [SerializeField]
//    private TMP_Text healthText;

//    private void OnEnable()
//    {
//        //1.订阅事件：当玩家血量改变时自动调用RefreshHealth方法
//        //不能卸载start里，因为Start只会执行一次，UI面板再游戏过程中会频繁关闭打开，start中UI被重新激活时就不会再相应血量变化，卸载OnEnbale里面每次面板被开启（SetActive(true)都会自动重新绑定事件并刷新最新数值
//        playerHealth.HealthChanged += RefreshHealth;
//        //2.初始化刷新：UI刚显示时，主动获取当前血量刷新一次界面
//        RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
//    }

//    private void OnDisable()
//    {
//        //3.取消订阅事件，防止内存泄露和空指针异常
//        //如果不取消订阅，当这个UI面板被销毁或隐藏后，playerHealth事件里依然留着对这个UI的引用，当玩家受击触发事件时代码就会试图更新一个不存在的UI对象，报空甚至引发内存泄露
//        playerHealth.HealthChanged -= RefreshHealth;
//    }

//    private  void RefreshHealth(int current,int max)
//    {
//        healthText.text = $"HP:{current}/{max}";
//    }
//}

