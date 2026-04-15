using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.15 오후 22:58
 *  마지막 수정 일자 : 26.04.15 오후 23:04
 *  
 *  [스크립트 목적 및 내용]
 *  1. 손에 든 아이템 시스템 - 플레이어가 손에 든 아이템을 관리하는 스크립트
 *    1-1. 퀵슬롯에서 선택된 아이템이 바뀔 때마다 손에 든 아이템을 갱신하는 역할
 *  
 *  2. 구현 중 고려해야 할 요소
 *    2-1. 아이템 위치 미세 조정 (Offset):
 *         - 칼은 손잡이를 잡아야 하고, 횃불은 아래쪽을 잡아야 합니다.
 *         - 각 아이템 프리팹 자체의 자식 오브젝트(Visual) 위치를 조절하여
 *           HandAnchor에 딱 맞게 세팅하세요.
 *    2-2. 레이어 순서 (Sorting Layer):
 *         - 아이템이 플레이어 몸 뒤로 가야 할 때와 앞으로 와야 할 때가 있습니다.
 *         - 플레이어의 바라보는 방향(상, 하, 좌, 우)에 따라
 *           SpriteRenderer.sortingOrder를 동적으로 조절하는 로직을
 *           PlayerHandController에 추가하면 좋습니다.
 *    2-3. 애니메이션 연동:
 *         - 플레이어가 걸을 때 손(HandAnchor)도 같이 위아래로 흔들리게
 *           애니메이션을 잡으면 아이템도 자연스럽게 같이 움직입니다
 *  
 *  3. 추후 고려 사항
 *    3-1. 현재는 단순히 이미지를 보여주는 기능
 *    3-2. 나중에 무기 휘두르기, 도구 사용, 소모품 먹기 등의 액션으로 연동 가능
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerHandController : MonoBehaviour
{
    [Header("# References")]
    [SerializeField] private QuickSlotManager quickSlotManager;
    [SerializeField] private Transform handAnchor; // 아이템이 붙을 위치

    [Header("# Current State")]
    private GameObject currentHandItem;
    private int currentItemId = -1;

    private void OnEnable()
    {
        // 퀵슬롯 선택 변경 이벤트 구독
        quickSlotManager.OnSlotSelected += RefreshHand;
    }

    private void OnDisable()
    {
        quickSlotManager.OnSlotSelected -= RefreshHand;
    }

    public void RefreshHand(int selectedIndex)
    {
        // 1. 선택된 슬롯의 아이템 정보 가져오기
        InventorySlot slot = quickSlotManager.GetSelectedSlot();

        // 2. 같은 아이템을 이미 들고 있다면 교체하지 않음 (최적화)
        if (currentItemId == slot.itemId) return;

        // 3. 기존에 들고 있던 오브젝트 파괴
        if (currentHandItem != null)
        {
            Destroy(currentHandItem);
            currentHandItem = null;
        }

        currentItemId = slot.itemId;

        // 4. 빈 슬롯이면 종료
        if (currentItemId <= 0) return;

        // 5. 아이템 데이터로부터 프리팹(외형) 생성
        Item itemData = ItemDatabase.Instance.GetItem(currentItemId);
        //if (itemData != null && itemData.itemPrefab != null)
        //{
        //    currentHandItem = Instantiate(itemData.itemPrefab, handAnchor);

        //    // 픽셀 아트 게임이라면 로컬 좌표 초기화가 중요합니다.
        //    currentHandItem.transform.localPosition = Vector3.zero;
        //    currentHandItem.transform.localRotation = Quaternion.identity;
        //}
    }
}
