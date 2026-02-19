using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.02.20 오전 02:02
 *  
 *  [스크립트 목적 및 내용]
 *  1. 무기의 '공격 방식'을 정의
 *    1-1. 공격 로직의 핵심
 *    1-2. 공격 로직은 비동기성이 중요함
 *    1-3. 그러므로, 코루틴 기반으로 설계하고 '판정' 부분을 분리함
 *    1-4. 모든 무기 로직은 AttackExecutor를 상속받아 구현됨
 *    1-5. 예시로 휘두르기, 발사하기, 장판 깔기 등
 *    
 *  2. 큰 그림
 *    - PlayerCombatController  // 단순 입력, WeaponController에게 명령을 내리기 위함
 *    - PlayerEquipment         // 장비 장착/해제 관리
 *      ├─ WeaponController     // 무기의 실체 관리 (장착, 해제)
 *      └─ Weapon               // 물리적인 무기
 *         ├─ WeaponItem        // 무기 아이템 정보
 *         └─ AttackExecutor    // 무기 공격 방식
 *            ├─ MeleeAttackExecutor    // 근접형 무기
 *            ├─ ToolAttackExecutor     // 도구형 무기
 *            ├─ RangedAttackExecutor   // 원거리 무기
 *            ├─ MagicAttackExecutor    // 마법 무기
 *            └─ SummonAttackExecutor   // 소환 무기
 *  
 *  3. 추후 고려해야 할 내용
 *    3-1. 공격 방향 (Directional Combat):
 *         - 코어 키퍼는 8방향 혹은 마우스 방향을 바라봅니다.
 *           AttackExecutor에서 공격 애니메이션을 실행할 때,
 *           현재 캐릭터의 LookDirection에 따라
 *           스프라이트를 회전시키거나 뒤집는 로직이 포함되어야 합니다.
 *     3-2. 공격 취소
 *          - 피격 당했을 때 (Stun) 공격 루틴을 멈추는 StopAttack() 기능이 필요
 *     3-3. 스테미나/마나 소비
 *          - TryAttack 단계에서 현재는 단순 쿨타임 체크만하지만,
 *            추후에 스테미나/마나 소비 기능을 추가하여야 함
 *     3-4. 애니메이션 이벤트 연동
 *          - AttackRoutine에서 단순히 yield return new WaitForSeconds를 쓰는 것보다,
 *          - 유니티 Animation Event를 통해
 *            "실제로 휘두르는 시점"에 판정을 내리는 것이 훨씬 타격감이 좋습니다.
 *          
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public abstract class AttackExecutor : MonoBehaviour
{
    protected WeaponItem weaponData;
    protected WeaponController controller;
    protected bool isAttacking;
    private float lastAttackTime;

    public virtual void Initialize(WeaponItem data, WeaponController ctrl)
    {
        weaponData = data;
        controller = ctrl;
    }

    public void TryAttack()
    {
        if (!CanAttack())
            return;

        StartAttack();
    }

    public bool CanAttack() => !isAttacking && Time.time >= lastAttackTime + weaponData.attackDelay;

    public void StartAttack()
    {
        lastAttackTime = Time.time;
        StartCoroutine(AttackRoutine());
    }

    // 현재 yield return new WaitForSeconds를 쓰는 것보다,
    // Unity Animation Event를 통해서 실제로 휘두르는 시점에 판정을 내리는 것이 목표
    protected abstract IEnumerator AttackRoutine();
}
