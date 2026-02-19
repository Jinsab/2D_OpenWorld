using UnityEngine;
using System.Collections;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 02:04
 *  마지막 수정 일자 : 26.02.20 오전 02:04
 *  
 *  [스크립트 목적 및 내용]
 *  1. 근접 무기 (검, 대검, 창 등)
 *    1-1. 공격 로직(방식)을 상속 받음
 *    1-2. 적을 감지하며 데미지를 입힘
 *          
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class MeleeAttackExecutor : AttackExecutor
{
    public float attackRange = 1.2f;
    public LayerMask enemyLayer;

    protected override IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // 1. 휘두르는 애니메이션 실행 (애니메이터 파라미터 트리거)
        // controller.GetComponent<Animator>().SetTrigger("Attack");

        // 2. 공격 판정 시점까지 대기 (프레임 단위 디테일)
        yield return new WaitForSeconds(weaponData.attackDelay * 0.3f);

        // 3. 범위 내 적 감지
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayer);

        foreach (var enemy in hitEnemies)
        {
            // IDamageable 인터페이스를 사용하는 적에게 데미지 전달
            // enemy.GetComponent<IDamageable>()?.TakeDamage(weaponData.attackDamage);
            Debug.Log($"{enemy.name}에게 {weaponData.attackDamage} 데미지!");
        }

        // 4. 나머지 쿨타임 대기
        yield return new WaitForSeconds(weaponData.attackDelay * 0.7f);
        isAttacking = false;
    }

    private void OnDrawGizmosSelected() // 에디터에서 공격 범위 확인용
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
