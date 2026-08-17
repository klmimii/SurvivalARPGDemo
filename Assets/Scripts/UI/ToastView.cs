using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// UI飘字提示框
/// </summary>
public class ToastView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text toastText;//界面上要显示的文字内容
    [SerializeField]
    private float showDuration = 1.5f;//提示框停在屏幕上的时间

    //声明协程
    private Coroutine hideCoroutine;

    private void Awake()
    {
        //原本写成gameObject.SetActive(fasle)这是不对的，因为相当于ToastView脚本在初始化阶段，把自己所在的GameObject关闭了，处于禁用重装
        //而外部仍然持有ToastView引用，这时候外部调用Show函数，虽然ToastVIew被关闭里，但是引用还在，所以他仍然可以被调用。而Show中让自己被激活，但是自己本身就是被关闭的对象，此时尝试重新激活自己，生命周期状态发生变化，StartCoroutines时对象仍处于inactive
        //导致协程启动失败，而第二次TosatText已经经历了一次Awake/Disable，Unity对象状态已经稳定
        //总结：一个对象如果需要长期接收调用，比如Toast,Manager，Controller等等，不要隐藏自己而是控制组建的失活激活
        toastText.enabled = false;
    }

    public void Show(string message)
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }


        toastText.text = message;

        toastText.enabled = true;


        hideCoroutine = StartCoroutine(HideAfterDelay());
    }


private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(showDuration);//等待1.5秒
        toastText.enabled = false;//隐藏UI
        hideCoroutine = null;//清楚句柄标记
    }
}
