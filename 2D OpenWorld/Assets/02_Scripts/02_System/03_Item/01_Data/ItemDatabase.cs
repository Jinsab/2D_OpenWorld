using UnityEngine;
using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.13 오후 20:46
 *  마지막 수정 일자 : 26.03.30 오후 16:38
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트 - 아이템 데이터베이스
 *    1-1. ItemID로 Item을 찾는 기능 제공
 *    1-2. Item으로 ItemSet을 찾는 기능 제공
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

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;
    public List<Item> allItems;

    // 프로젝트 내의 모든 세트 아이템 에셋을 여기에 드래그 앤 드롭으로 등록합니다.
    public List<ItemSet> allSets = new List<ItemSet>();

    private Dictionary<int, Item> itemDict;

    void Awake()
    {
        Instance = this;

        itemDict = new Dictionary<int, Item>();

        foreach (var item in allItems)
        {
            itemDict[item.itemId] = item;
        }
    }

    public Item GetItem(int id)
    {
        if (itemDict.TryGetValue(id, out Item item))
            return item;

        return null;
    }

    public bool TryGetItem(int id, out Item result)
    {
        if (itemDict.TryGetValue(id, out Item item))
            result = item;
        else
            result = null;

        return result != null;
    }

    // 특정 아이템 ID나 이름으로 세트를 찾는 기능 등을 추가할 수 있습니다.
    public bool TryFindSetByItem(Item item, out ItemSet itemSet)
    {
        itemSet = allSets.Find(s => s.requiredItems.Contains(item));

        return itemSet != null;
    }
}
