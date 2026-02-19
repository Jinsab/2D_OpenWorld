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
 *  1. 캐릭터의 비주얼을 담당함
 *    1-1. 장비 장착 시 스프라이트 업데이트
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterVisuals : MonoBehaviour
{
    [System.Serializable]
    public struct BodyParts
    {
        public SpriteRenderer head;
        public SpriteRenderer chest;
        public SpriteRenderer pants;
        public SpriteRenderer weapon;
    }

    public BodyParts renderers;

    // 장비 장착 시 호출
    public void UpdateVisual(EquipmentSlot slot, Sprite newSprite)
    {
        SpriteRenderer target = GetRenderer(slot);
        if (target != null)
        {
            target.sprite = newSprite;
            // 팁: 장착 시 반짝이는 이펙트나 소리를 여기서 추가할 수 있습니다.
        }
    }

    // 장비 해제 시 호출
    public void ClearVisual(EquipmentSlot slot)
    {
        SpriteRenderer target = GetRenderer(slot);
        if (target != null) target.sprite = null;
    }

    private SpriteRenderer GetRenderer(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => renderers.head,
            EquipmentSlot.Chest => renderers.chest,
            EquipmentSlot.Pants => renderers.pants,
            EquipmentSlot.Weapon => renderers.weapon,
            _ => null
        };
    }
}
