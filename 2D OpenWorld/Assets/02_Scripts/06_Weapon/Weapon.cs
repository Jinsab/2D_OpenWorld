using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.02.20 오전 02:01
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 손에 실제로 생성되는 '물리적인 무기'의 실체
 *    1-1. 무기의 외형을 담당함 (스프라이트롤 보여줌)
 *    1-2. 공격 판정 위치를 담당함 (이펙트가 나갈 위치)
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class Weapon : MonoBehaviour
{
    [Header("Components")]
    public SpriteRenderer weaponRenderer; // 무기 이미지 출력
    public Transform firePoint;           // 화살 발사나 이펙트 생성 위치

    protected WeaponItem data;
    protected AttackExecutor executor;

    public virtual void Initialize(WeaponItem weaponData, WeaponController controller)
    {
        data = weaponData;
        weaponRenderer.sprite = data.equipmentSprite; // 무기 외형 설정

        // 해당 오브젝트에 붙어있는 AttackExecutor(Swing, Shoot 등)를 찾아 초기화
        executor = GetComponent<AttackExecutor>();
        if (executor != null)
            executor.Initialize(data, controller);
    }

    public void RequestAttack() => executor?.TryAttack();
}
