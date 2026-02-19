using UnityEngine;
using UnityEngine.InputSystem;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 17:40
 *  마지막 수정 일자 : 26.02.19 오후 17:40
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 스탯 스크립트
 *    1-1. 
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

public class PlayerController : MonoBehaviour
{
    [Header("# References")]
    [field: SerializeField] public PlayerSO Data { get; private set; }

    [Header("# Player Info")]
    public PlayerInput Input;
    public Rigidbody2D Rigidbody;
    public Animator Animator;

    [Header("# Player Data")]
    [field: SerializeField] public PlayerMovement PlayerMovement { get; private set; }
    [field: SerializeField] public PlayerLook PlayerLook { get; private set; }
    [field: SerializeField] public PlayerStateMachine stateMachine { get; private set; }

    [Header("# Animations")]
    [field: SerializeField] public PlayerAnimation AnimationData { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        InputEnabled();
    }

    private void OnDisable()
    {
        InputDisable();
    }

    private void Initialize()
    {
        Input = GetComponent<PlayerInput>();
        Rigidbody = GetComponent<Rigidbody2D>();
        Animator = GetComponentInChildren<Animator>();
        PlayerMovement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();

        PlayerMovement.Initialize();
        stateMachine.Initialize();
    }

    public void InputEnabled()
    {
        Input.enabled = true;
    }

    public void InputDisable()
    {
        Input.enabled = false;
    }
}