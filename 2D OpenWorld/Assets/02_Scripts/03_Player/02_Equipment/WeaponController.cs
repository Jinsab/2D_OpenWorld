using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.02.20 오전 01:42
 *  
 *  [스크립트 목적 및 내용]
 *  1. 장착된 무기의 실체(GameObject)를 관리
 *    1-1. 현재 장착된 Weapon 오브젝트를 생성하고 관리
 *    1-2. 공격 입력을 Weapon에게 전달함
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class WeaponController : MonoBehaviour
{
    public Transform weaponHolder; // 무기가 생성될 위치 (주로 플레이어의 손)
    private Weapon _currentWeapon;

    public void EquipWeapon(WeaponItem data)
    {
        UnEquipWeapon(); // 기존 무기 제거

        if (data.weaponPrefab == null) return;

        // 무기 프리팹 생성 및 초기화
        GameObject obj = Instantiate(data.weaponPrefab, weaponHolder);
        _currentWeapon = obj.GetComponent<Weapon>();
        _currentWeapon.Initialize(data, this);
    }

    public void UnEquipWeapon()
    {
        if (_currentWeapon != null)
        {
            Destroy(_currentWeapon.gameObject);
            _currentWeapon = null;
        }
    }

    // CombatController에서 공격 입력 시 호출
    public void HandleAttack()
    {
        if (_currentWeapon != null)
        {
            _currentWeapon.RequestAttack();
        }
    }
}