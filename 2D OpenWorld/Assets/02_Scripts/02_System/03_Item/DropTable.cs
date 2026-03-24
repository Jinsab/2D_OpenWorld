using UnityEngine;
using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 18:20
 *  마지막 수정 일자 : 26.03.24 오후 19:39
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 시스템 - 드랍 테이블
 *    1-1. 드롭되는 아이템 목록
 *    1-2. 확률에 따라 아이템 드롭
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

[CreateAssetMenu(menuName = "Item/DropTable")]
public class DropTable : ScriptableObject
{
    public List<DropData> drops;

    public void Drop(Vector3 position)
    {
        foreach (var drop in drops)
        {
            if (Random.value <= drop.dropChance)
            {
                int amount = Random.Range(
                    drop.minAmount,
                    drop.maxAmount + 1
                );

                ItemDropSpawner.Instance.Spawn(
                    drop.item,
                    amount,
                    position,
                    true
                );
            }
        }
    }
}