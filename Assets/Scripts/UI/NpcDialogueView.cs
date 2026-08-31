using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialogueView : MonoBehaviour
{
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryButtonText;
    [SerializeField] private Button closeButton;

    public event Action PrimaryClicked;


    private UIPanelTween panelTween;
    public bool IsVisible => panelTween != null
        ? panelTween.IsVisible
        : gameObject.activeInHierarchy;
    public event Action Closed;

    private void Awake()
    {
        panelTween = UIPanelTween.GetOrAdd(gameObject);
        primaryButton.onClick.AddListener(() => PrimaryClicked?.Invoke());
        closeButton.onClick.AddListener(Close);
    }

    public void Show(string npcName, string content, string primaryText, bool showPrimaryButton)
    {
        npcNameText.text = npcName;
        contentText.text = content;
        primaryButton.gameObject.SetActive(showPrimaryButton);
        primaryButtonText.text = primaryText;
        UIPanelTween.GetOrAdd(gameObject).Show();
    }

    public void Close()
    {
        UIPanelTween.GetOrAdd(gameObject).Hide();
        Closed?.Invoke();
    }
}