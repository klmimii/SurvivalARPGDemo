using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PartyHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text characterNameText;

    public void Render(CharacterDefinition definition)
    {
        characterNameText.text = definition == null ? string.Empty : definition.displayName;
    }
}
