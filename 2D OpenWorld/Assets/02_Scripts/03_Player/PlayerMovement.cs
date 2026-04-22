using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.12 오후 20:48
 *  마지막 수정 일자 : 26.04.22 오후 20:47
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 이동 스크립트
 *    1-1. 입력 값에 따라 플레이어 이동
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerMovement : MonoBehaviour
{
    [Header("# Move Data")]
    public Vector2 MoveInput { get; private set; }
    public float MovementSpeed { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }
    public float currentSpeed;

    [Header("# Player Render Data")]
    public int PlayerOrder { get { return playerRenderer[playerRenderer.Length - 1].sortingOrder; } }
    private SpriteRenderer[] playerRenderer;
    private SpriteRenderer[] shadowRenderer;

    // Player Controller
    private PlayerController Player;

    // Player Input System
    private InputAction moveAction;
    private InputAction runAction;

    // Sorting Data
    private int sortingY;
    private float sortingZ;
    private Vector3 lastPos;

    public void Initialize()
    {
        Player = GetComponent<PlayerController>();
        MovementSpeed = Player.Data.GroundedData.BaseSpeed;
        playerRenderer = Player.transform.GetChild(0).GetComponentsInChildren<SpriteRenderer>();
        shadowRenderer = Player.transform.GetChild(1).GetComponentsInChildren<SpriteRenderer>();

        moveAction = Player.Input.actions["Move"];
        runAction = Player.Input.actions["Sprint"];
        //jumpAction = player.playerInput.actions["Jump"];

        // 이동 이벤트
        moveAction.performed += ctx =>
        {
            MoveInput = ctx.ReadValue<Vector2>();
            IsMoving = true;
            //moveVector = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            //isMove = moveVector.magnitude > 0;
        };
        moveAction.canceled += ctx =>
        {
            MoveInput = Vector2.zero;
            IsMoving = false;
            //moveVector = Vector3.zero;
            //isMove = false;
        };

        // 달리기 이벤트
        runAction.started += ctx => { IsRunning = true; };
        runAction.canceled += ctx => { IsRunning = false; };
    }

    private void FixedUpdate()
    {
        if (UIManager.Instance.CurrentState != UIManager.UIState.None)
            return;

        if (Player.StateMachine.CurrentState == PlayerState.Idle ||
            Player.StateMachine.CurrentState == PlayerState.Attack ||
            Player.StateMachine.CurrentState == PlayerState.Stun)
        {
            currentSpeed = 0f;
        }
        else
        {
            currentSpeed = Player.StateMachine.CurrentState ==
            PlayerState.Run ? MovementSpeed * Player.Data.GroundedData.RunSpeedModifier :
                              MovementSpeed * Player.Data.GroundedData.WalkSpeedModifier;
        }

        Vector2 move = new Vector3(MoveInput.x, MoveInput.y).normalized;
        move = Player.Rigidbody.position + move * currentSpeed * Time.fixedDeltaTime;

        sortingZ = SortingOrderUtility.UpdateSortingZ(transform) + 1f;
        Player.Rigidbody.MovePosition(new Vector3(move.x, move.y, sortingZ));
        transform.position = new Vector3(transform.position.x, transform.position.y, sortingZ);
    }

    private void LateUpdate()
    {
        if (UIManager.Instance.CurrentState != UIManager.UIState.None || MoveInput != Vector2.zero)
        {
            sortingY = SortingOrderUtility.UpdateSortingY(transform);

            // 그림자는 항상 플레이어 뒤에 있어야 하기 때문에 1을 빼야 함
            for (int i = 0; i < playerRenderer.Length; i++)
            {
                playerRenderer[i].sortingOrder = sortingY + (playerRenderer.Length - 1 - i);
            }

            for (int i = 0; i < shadowRenderer.Length; i++)
            {
                shadowRenderer[i].sortingOrder = sortingY - 1;
            }
        }
    }
}
