using UnityEngine;

/// <summary>
/// 挂在 Animator 动画状态上的 SMB，动画退出时通知 CharacterBehaviour。
/// 使用方式：在 Animator Controller 中选中需要通知结束的动画状态 → Add Behaviour → StateFinishNotifier。
/// </summary>
public class StateFinishNotifier : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var character = animator.GetComponentInParent<CharacterBehaviour>();
        if (character != null)
            character.NotifyAnimStateFinished();
    }
}
