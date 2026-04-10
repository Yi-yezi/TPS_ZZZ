using UnityEngine;
using GamePlay.Shared;
using Input;

/// <summary>
/// 角色行为控制，负责输入采集与分层状态机（HFSM）驱动。
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(InputHandler))]
public class CharacterBehaviour : MonoBehaviour
{
	#region 字段

	[Header("Movement")]
	[SerializeField] private float rotationSpeed = 720f;
	[SerializeField] private float gravity = -9.81f;

	[Header("Refs")]
	[SerializeField] private Animator animator;
	[SerializeField] private CharacterController characterController;
	[SerializeField] private Camera mainCamera;

	private Health health;
	private Damageable damageable;
	private InputHandler inputHandler;

	private CharacterState characterState;

	#endregion

	#region 属性

	public Health Health => health;
	public Damageable Damageable => damageable;
	public InputHandler InputHandler => inputHandler;
	public bool IsDead => health != null && health.IsDead;
	public Vector2 MoveInput => inputHandler != null ? inputHandler.CurrentFrame.MoveVector : Vector2.zero;
	public float MoveInputMagnitude => MoveInput.magnitude;
	public float RotationSpeed => rotationSpeed;
	public Animator Animator => animator;
	public CharacterStateId CurrentStateId => characterState.CurrentStateId;
	public CharacterState CharacterState => characterState;

	#endregion

	#region 生命周期

	private void Awake()
	{
		health = GetComponent<Health>();
		damageable = GetComponent<Damageable>();
		inputHandler = GetComponent<Input.InputHandler>();

		animator = GetComponent<Animator>();
		characterController = GetComponent<CharacterController>();
		mainCamera = Camera.main;

		if (health != null)
			health.OnDeath += HandleDeath;
		if (damageable != null)
			damageable.OnDamageReceived += HandleHit;

		SetupStateMachine();
	}

	private void Update()
	{
		inputHandler?.Tick();
		characterState.Update(Time.deltaTime);
	}

	private void FixedUpdate()
	{
		characterState.FixedUpdate(Time.fixedDeltaTime);
	}

	private void OnAnimatorMove()
	{
		if (animator == null) return;

		Vector3 deltaPos = animator.deltaPosition;
		deltaPos.y += gravity * Time.deltaTime;
		characterController.Move(deltaPos);
		transform.rotation *= animator.deltaRotation;
	}

	#endregion

	#region 状态机

	private void SetupStateMachine()
	{
		characterState = new CharacterState(this);
		characterState.OnEnter();
	}

	/// <summary>
	/// 由 StateFinishNotifier SMB 调用，遍历到当前叶子状态并标记完成。
	/// </summary>
	public void NotifyAnimStateFinished()
	{
		IState state = characterState.CurrentState;
		while (state?.CurrentSubState != null)
			state = state.CurrentSubState;
		state?.MarkFinished();
	}

	#endregion

	#region 事件回调

	private void HandleDeath()
	{
		characterState.RequestState(CharacterStateId.Dead);
	}

	private void HandleHit(int damage, UnityEngine.GameObject attacker)
	{
		if (characterState.CurrentStateId == CharacterStateId.Dead)
			return;
		characterState.RequestState(CharacterStateId.Hit);
	}

	#endregion

	#region 移动与旋转

	public Vector3 GetMoveDirection()
	{
		if (inputHandler == null) return Vector3.zero;

		Input.InputFrame frame = inputHandler.CurrentFrame;
		if (!frame.HasMovement) return Vector3.zero;

		Vector3 forward = transform.forward;
		Vector3 right   = transform.right;

		if (mainCamera != null)
		{
			Vector3 camForward = mainCamera.transform.forward;
			camForward.y = 0f;
			camForward.Normalize();

			Vector3 camRight = mainCamera.transform.right;
			camRight.y = 0f;
			camRight.Normalize();

			forward = camForward;
			right   = camRight;
		}

		Vector2 input = frame.MoveVector;
		return right * input.x + forward * input.y;
	}

	public void RotateTowards(Vector3 direction, float deltaTime)
	{
		if (direction.sqrMagnitude <= 0.0001f)
			return;

		direction.Normalize();
		FaceDirection(direction, deltaTime);
	}

	public void FaceDirection(Vector3 direction, float deltaTime)
	{
		if (direction.sqrMagnitude <= 0.0001f)
		{
			return;
		}

		Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
		transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * deltaTime);
	}

	#endregion
}
