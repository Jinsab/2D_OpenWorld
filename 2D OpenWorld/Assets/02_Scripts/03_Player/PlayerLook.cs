using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.12 오후 20:50
 *  마지막 수정 일자 : 26.03.22 오후 19:38
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 회전
 *    1-1. 마우스 위치에 따른 보는 방향 값
 *    1-2. 보는 방향에 따른 스프라이트 변경
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public enum LookDirection
{
    Down,
    Up,
    Left,
    Right
}

public class PlayerLook : MonoBehaviour
{
    [Header(" # Player State")]
    public PlayerStateMachine playerStateMachine;

    [Header(" # Look Direction")]
    public LookDirection CurrentLookDirection { get; private set; }
    
    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (UIManager.Instance.CurrentState == UIManager.UIState.None)
            UpdateLookDirection();
    }

    private void UpdateLookDirection()
    {
        if (playerStateMachine.CurrentState == PlayerState.Attack ||
            playerStateMachine.CurrentState == PlayerState.Stun)
        {
            // 공격 중이거나, 스턴 상태에서는 회전해서는 안됨
        }
        else
        {
            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            Vector2 dir = (mouseWorld - transform.position);

            dir.Normalize();

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                CurrentLookDirection = dir.x > 0 ? LookDirection.Right : LookDirection.Left;
            }
            else
            {
                if (dir.y > 0)
                    CurrentLookDirection = LookDirection.Up;
                else
                    CurrentLookDirection = LookDirection.Down;
            }
        }
    }
}