using System;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.12 오후 20:52
 *  마지막 수정 일자 : 26.02.12 오후 20:52
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 애니메이션
 *    1-1. 애니메이션 관리
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

[Serializable]
public class PlayerAnimation : MonoBehaviour
{
    [Header("# Player")]
    [SerializeField] private PlayerController Player;

    [Header("# Parameter")]
    [SerializeField] private string stateParameterName = "State";
    [SerializeField] private string directionParameterName = "Direction";

    [Header("# Ground")]
    [SerializeField] private string idleParameterName = "Idle";
    [SerializeField] private string walkParameterName = "Walk";
    [SerializeField] private string runParameterName = "Run";
    [SerializeField] private string dodgeParameterName = "Dodge";

    [SerializeField] private string attackParameterName = "Attack";

    public int StateParameterHash { get; private set; }
    public int DirectionParameterHash { get; private set; }

    public int IdleParameterHash { get; private set; }
    public int WalkParameterHash { get; private set; }
    public int RunParameterHash { get; private set; }
    public int DodgeParameterHash { get; private set; }

    public int AttackParameterHash { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        Player.Animator.SetInteger(StateParameterHash, (int)Player.StateMachine.CurrentState);
        Player.Animator.SetFloat(DirectionParameterHash, (int)Player.PlayerLook.CurrentLookDirection);
    }

    public void Initialize()
    {
        StateParameterHash = Animator.StringToHash(stateParameterName);
        DirectionParameterHash = Animator.StringToHash(directionParameterName);

        IdleParameterHash = Animator.StringToHash(idleParameterName);
        WalkParameterHash = Animator.StringToHash(walkParameterName);
        RunParameterHash = Animator.StringToHash(runParameterName);

        DodgeParameterHash = Animator.StringToHash(dodgeParameterName);

        AttackParameterHash = Animator.StringToHash(attackParameterName);
    }
}