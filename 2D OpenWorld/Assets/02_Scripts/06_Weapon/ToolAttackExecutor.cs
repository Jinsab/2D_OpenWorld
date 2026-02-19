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
 *  1. 도구 무기 (곡괭이, 도끼 등)
 *    1-1. 공격 로직(방식)을 상속 받음
 *    1-2. 타일을 파괴하거나 자원을 채집하는 로직
 *          
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class ToolAttackExecutor : AttackExecutor
{
    public float toolRange = 1.5f;
    public LayerMask resourceLayer; // 광석, 나무 등 자원 레이어

    protected override IEnumerator AttackRoutine()
    {
        isAttacking = true;

        yield return new WaitForSeconds(weaponData.attackDelay * 0.2f);

        // 마우스 방향 혹은 캐릭터 전방의 타일/자원 감지
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 targetDir = (mousePos - transform.position).normalized;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, targetDir, toolRange, resourceLayer);

        if (hit.collider != null)
        {
            // 자원 객체에게 채취 명령 (예: 광석 체력 감소)
            // hit.collider.GetComponent<IHarvestable>()?.OnHarvest(weaponData.attackDamage);
            Debug.Log($"{hit.collider.name} 채취 시도!");
        }

        yield return new WaitForSeconds(weaponData.attackDelay * 0.8f);
        isAttacking = false;
    }
}
