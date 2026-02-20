using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.12 오후 20:50
 *  마지막 수정 일자 : 26.02.12 오후 20:50
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 회전
 *    1-1. 마우스 위치에 따른 보는 방향 값
 *    1-2. 보는 방향에 따른 스프라이트 변경
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

public enum LookDirection
{
    Down,
    Up,
    Side
}

public class PlayerLook : MonoBehaviour
{
    [Header(" # Player State")]
    public PlayerStateMachine playerStateMachine;

    [Header(" # Look Direction")]
    public LookDirection CurrentLookDirection { get; private set; }
    
    private Camera mainCam;
    private SpriteRenderer[] spriteRenderers;

    void Awake()
    {
        mainCam = Camera.main;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
    }

    void Update()
    {
        UpdateLookDirection();
    }

    private void UpdateLookDirection()
    {
        if (playerStateMachine.CurrentState == PlayerState.Attack)
        {
            if (CurrentLookDirection == LookDirection.Up ||
                CurrentLookDirection ==LookDirection.Down)
            {
                transform.localScale = Vector3.one;

                //for (int i = 0; i < spriteRenderers.Length; i++)
                //{
                //    spriteRenderers[i].flipX = true;
                //}
            }
        }
        else if (playerStateMachine.CurrentState == PlayerState.Stun)
        {
            // 스턴 중엔 회전해선 안됨
        }
        else
        {
            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            Vector2 dir = (mouseWorld - transform.position);

            dir.Normalize();

            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                CurrentLookDirection = LookDirection.Side;

                transform.localScale = dir.x > 0 ? new Vector3(-1, 1, 1) : Vector3.one;

                //for (int i = 0; i < spriteRenderers.Length; i++)
                //{
                //    spriteRenderers[i].flipX = dir.x > 0;
                //}
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