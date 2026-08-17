using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossHudPresenter : MonoBehaviour
{
    [Header("Boss HUD")]
    [SerializeField] private string bossName = "森林守卫";
    [SerializeField] private BossHudView bossHudView;

    [Header("显示距离")]
    [Min(0.1f)]
    [SerializeField] private float showDistance = 18f;

    [Tooltip("应大于Show Distance，避免边缘反复显示隐藏。")]
    [Min(0.1f)]
    [SerializeField] private float hideDistance = 22f;

    [Tooltip("进入显示范围后持续多久才显示。")]
    [Min(0f)]
    [SerializeField] private float showDelay = 1f;

    private Health health;
    private Transform player;
    private float timeInsideShowRange;
    private bool isVisible;

    private void Awake()
    {
        health = GetComponent<Health>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void OnEnable()
    {
        health.HealthChanged += Refresh;
        health.Died += HandleDied;

        isVisible = false;
        timeInsideShowRange = 0f;

        if (bossHudView != null)
        {
            bossHudView.Hide();
        }
    }

    private void OnDisable()
    {
        health.HealthChanged -= Refresh;
        health.Died -= HandleDied;

        if (bossHudView != null)
        {
            bossHudView.Hide();
        }
    }

    private void Update()
    {
        if (health.IsDead || player == null)
        {
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position);

        if (!isVisible)
        {
            UpdateHiddenState(distance);
            return;
        }

        if (distance >= hideDistance)
        {
            Hide();
        }
    }

    private void UpdateHiddenState(float distance)
    {
        if (distance > showDistance)
        {
            timeInsideShowRange = 0f;
            return;
        }

        timeInsideShowRange += Time.deltaTime;

        if (timeInsideShowRange >= showDelay)
        {
            Show();
        }
    }

    private void Show()
    {
        isVisible = true;
        timeInsideShowRange = 0f;

        bossHudView.Show(
            bossName,
            health.CurrentHealth,
            health.MaxHealth);
    }

    private void Refresh(int current, int max)
    {
        // HUD隐藏期间不用刷新画面；Show时会读取最新Health。
        if (isVisible)
        {
            bossHudView.Refresh(current, max);
        }
    }

    private void Hide()
    {
        isVisible = false;
        timeInsideShowRange = 0f;
        bossHudView.Hide();
    }

    private void HandleDied()
    {
        Hide();
    }
}

//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 把boss体内的生命值数据和屏幕上的UI血条控件连接起来 
///// </summary>
//[RequireComponent(typeof(Health))]
//public class BossHudPresenter : MonoBehaviour
//{
//    [SerializeField] private string bossName = "森林守卫";
//    [SerializeField] private BossHudView bossHudView;

//    private Health health;

//    private void Awake()
//    {
//        health = GetComponent<Health>();
//    }

//    private void OnEnable()
//    {
//        //订阅事件，当血量改变时调用refresh，死亡时调用Hide
//        health.HealthChanged += Refresh;
//        health.Died += Hide;
//        bossHudView.Show(bossName, health.CurrentHealth, health.MaxHealth);
//    }

//    private void OnDisable()
//    {
//        health.HealthChanged -= Refresh;
//        health.Died -= Hide;
//    }

//    private void Refresh(int current, int max)
//    {
//        bossHudView.Refresh(current, max);
//    }

//    private void Hide()
//    {
//        bossHudView.Hide();
//    }
//}
