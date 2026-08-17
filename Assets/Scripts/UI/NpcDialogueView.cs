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
    public event Action Closed;

    private void Awake()
    {
        primaryButton.onClick.AddListener(() => PrimaryClicked?.Invoke());
        closeButton.onClick.AddListener(Close);
    }

    public void Show(string npcName, string content, string primaryText, bool showPrimaryButton)
    {
        gameObject.SetActive(true);
        npcNameText.text = npcName;
        contentText.text = content;
        primaryButton.gameObject.SetActive(showPrimaryButton);
        primaryButtonText.text = primaryText;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        Closed?.Invoke();
    }
}