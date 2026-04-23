using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 02:09
 *  마지막 수정 일자 : 26.04.23 오후 17:30
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 공격 스크립트
 *    1-1. 공격 명령 전달
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[RequireComponent(typeof(PlayerController))]
public class PlayerCombatController : MonoBehaviour
{
    [Header("# Combat Data")]
    public WeaponItem testWeapon;
    public WeaponController weaponController;
    private PlayerController Player;
    public bool isAttack = false;

    // Player Input System
    private InputAction attackAction;   // 왼쪽 클릭   (일반 공격)
    private InputAction specialAction;  // 오른쪽 클릭 (특수 공격)

    public void Initialize()
    {
        Player = GetComponent<PlayerController>();

        attackAction = Player.Input.actions["Attack"];
        specialAction = Player.Input.actions["SpecialAttack"];

        // 일반 공격 이벤트
        //attackAction.performed += ctx =>
        //{

        //};
    }

    private void Update()
    {
        if (attackAction.IsPressed())
            AttackAnimation();

        //weaponController.HandleAttack();
    }

    public void AttackAnimation()
    {
        if (weaponController == null)
            return;

        if (!isAttack)
        {
            isAttack = true;
            Player.Animator.SetTrigger(Player.AnimationData.AttackParameterHash);
        }
    }

    public void OnAttackAnimationFinished()
    {
        isAttack = false;

        Log.Game("이벤트로 확인한 공격 애니메이션 종료");
    }
}
