using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using static UnityEditor.Experimental.GraphView.GraphView;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.12 오후 20:48
 *  마지막 수정 일자 : 26.02.13 오후 23:44
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 이동 스크립트
 *    1-1. 입력 값에 따라 플레이어 이동
 *    
 *  2. 큰 그림
 *    - Player (GameObject)
 *      ├─ PlayerController    // (구현) 입력 & 상태 조율 (두뇌)
 *      ├─ PlayerMovement      // (구현) 이동
 *      ├─ PlayerLook          // (구현) 회전
 *      ├─ PlayerInteraction   // 상호작용
 *      ├─ PlayerEquipment     // 장비 시스템
 *      ├─ PlayerCombat        // 전투 시스템
 *      │  ├─ CombatController // 공격 로직 관리
 *      │  └─ WeaponController // 무기 장착 관리
 *      │
 *      ├─ PlayerHealth        // (IDamageable) 체력 / 피격 / 사망
 *      ├─ PlayerStats         // 세부 스탯 (기력 등)
 *      ├─ Inventory           // 아이템 수집 / 관리
 *      ├─ PlayerItemUser      // 아이템 사용
 *      ├─ EffectController    // (예정) 효과 적용
 *      ├─ PlayerAnimation     // (구현) Animator 제어
 *      ├─ PlayerStateMachine  // (구현) 상태 관리
 *      └─ PlayerAudio / VFX   // (선택)
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerMovement : MonoBehaviour
{
    [Header("# Move Data")]
    public Vector2 MoveInput { get; private set; }
    public float MovementSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public float currentSpeed;

    // Player
    private PlayerController Player;
    private SpriteRenderer playerRenderer;
    private SpriteRenderer shadowRenderer;

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
        playerRenderer = GetComponent<SpriteRenderer>();
        shadowRenderer = Player.transform.GetChild(0).GetComponent<SpriteRenderer>();

        moveAction = Player.Input.actions["Move"];
        runAction = Player.Input.actions["Sprint"];
        //jumpAction = player.playerInput.actions["Jump"];

        // 이동 이벤트
        moveAction.performed += ctx =>
        {
            MoveInput = ctx.ReadValue<Vector2>();

            //moveVector = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            //isMove = moveVector.magnitude > 0;
        };
        moveAction.canceled += ctx =>
        {
            MoveInput = Vector2.zero;
            //moveVector = Vector3.zero;
            //isMove = false;
        };

        //// 달리기 이벤트
        runAction.started += ctx => { IsRunning = true; };
        runAction.canceled += ctx => { IsRunning = false; };
    }

    private void FixedUpdate()
    {
        if (Player.stateMachine.CurrentState == PlayerState.Idle)
        {
            currentSpeed = 0f;
        }
        else
        {
            currentSpeed = Player.stateMachine.CurrentState ==
            PlayerState.Run ? MovementSpeed * Player.Data.GroundedData.RunSpeedModifier :
                              MovementSpeed * Player.Data.GroundedData.WalkSpeedModifier;
        }

        Vector2 move = new Vector3(MoveInput.x, MoveInput.y).normalized;
        move = Player.Rigidbody.position + move * currentSpeed * Time.fixedDeltaTime;

        sortingZ = SortingOrderUtility.UpdateSortingZ(transform) + 1f;
        Player.Rigidbody.MovePosition(new Vector3(move.x, move.y, sortingZ));
    }

    private void LateUpdate()
    {
        if (MoveInput != Vector2.zero)
        {
            sortingY = SortingOrderUtility.UpdateSortingY(transform);

            // 그림자는 항상 플레이어 뒤에 있어야 하기 때문에 1을 빼야 함
            playerRenderer.sortingOrder = sortingY;
            shadowRenderer.sortingOrder = sortingY - 1;
        }
    }
}
