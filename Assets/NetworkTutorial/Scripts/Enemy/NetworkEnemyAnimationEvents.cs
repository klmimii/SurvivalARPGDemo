using UnityEngine;

public sealed class NetworkEnemyAnimationEvents : MonoBehaviour
{
    [SerializeField]
    private NetworkEnemyAI enemyAI;

    public void AnimationEvent_AttackHit()
    {
        enemyAI?.AnimationEvent_AttackHit();
    }

    public void AnimationEvent_AttackFinished()
    {
        enemyAI?.AnimationEvent_AttackFinished();
    }
}