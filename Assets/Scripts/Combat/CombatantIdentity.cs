using UnityEngine;

public class CombatantIdentity : MonoBehaviour
{
    [SerializeField] private CombatFaction faction;

    [Tooltip("伤害数字出现位置。为空时使用当前对象Transform。")]
    [SerializeField] private Transform feedbackAnchor;

    public CombatFaction Faction => faction;

    public Vector3 FeedbackPosition => feedbackAnchor != null
        ? feedbackAnchor.position
        : transform.position;
}