using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 21:15
 *  마지막 수정 일자 : 26.02.14 오후 21:26
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 시스템 - 도구 아이템
 *    1-1. 도구 데미지
 *    1-2. 도구 사용
 *    
 *  2. 큰 그림
 *    - Item (ScriptableObject)
 *      ├─ ItemData (기본 정보)
 *      ├─ ItemDatabase (데이터베이스)
 *      ├─ (Type)Item (아이템 타입)
 *      │  ├─ ConsumableItem (소비 아이템)
 *      │  └─ ToolItem (도구 아이템)
 *      │
 *      ├─ ItemDropSpawner
 *      ├─ ItemDrop
 *      ├─ DropTable
 *      └─ DropData
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[CreateAssetMenu(menuName = "Items/Equipment/Tool")]
public class ToolItem : EquipmentItem
{
    //public int power = 1;

    //public override void Use(GameObject user)
    //{
    //    ToolController controller = user.GetComponent<ToolController>();

    //    if (controller != null)
    //    {
    //        controller.UseTool(this);
    //    }
    //}
}
