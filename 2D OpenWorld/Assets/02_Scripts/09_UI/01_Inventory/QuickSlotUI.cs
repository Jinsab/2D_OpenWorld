using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.11 오후 20:50
 *  마지막 수정 일자 : 26.04.15 오후 17:53
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 UI 관리 스크립트
 *    1-1. Action Map - Player와 UI 각각에 맞게 이벤트를 연결함
 *    1-2. UIManager의 함수를 키 입력에 따라 직접적으로 호출함
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class QuickSlotUI : MonoBehaviour
{
    [Header("# References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private RectTransform selectionFrame; // 선택 강조 이미지
    [SerializeField] private Transform slotParent;        // 슬롯들이 담긴 부모

    private List<RectTransform> slotRects = new List<RectTransform>();
    [SerializeField] private SerializedDictionary<int, InventorySlotUI> quickSlotUIs = new();
    private int currentIdx = 0;

    private void OnEnable()
    {
        playerInventory.OnSlotChanged += UpdateSlotIcon;
    }

    private System.Collections.IEnumerator Start()
    {
        // Canvas.ForceUpdateCanvases();

        // 1. 자식 슬롯들의 RectTransform을 리스트에 담아 위치 추적 준비
        foreach (Transform child in slotParent)
        {
            slotRects.Add(child as RectTransform);
        }

        yield return new WaitForEndOfFrame(); // 레이아웃이 완전히 잡힐 때까지 대기
        InitializeUI();
    }

    private void InitializeUI()
    {
        // 초기 위치 설정
        UpdateSelectionFrame(0, true);
    }

    private void UpdateSlotIcon(int index)
    {
        // 퀵슬롯 범위(0~9) 내의 아이템이 바뀐 경우에만 UI 갱신
        if (index >= 0 && index < 10)
        {
            var data = playerInventory.inventoryData.slots[index];

            if (!quickSlotUIs.ContainsKey(index))
            {
                quickSlotUIs[index] = slotRects[index].GetComponent<InventorySlotUI>();
            }

            quickSlotUIs[index].scaleFlag = false;
            quickSlotUIs[index].UpdateVisual(data);
            AudioManager.Instance.Play(SND.UI_Item_Drop);
            // Log.UI($"{index}번 퀵슬롯 갱신 완료 (ID: {data.itemId}, Qty: {data.amount})");
        }
    }

    /// <summary>
    /// 선택된 슬롯으로 프레임을 이동시킵니다.
    /// </summary>
    public void UpdateSelectionFrame(int index, bool immediate = false)
    {
        // Log.UI("퀵슬롯 변경 로직 수행: " + index);
        currentIdx = index;
        Vector3 targetPos = slotRects[index].anchoredPosition;
        // Log.UI("목표 위치: " + targetPos);
        if (immediate)
        {
            selectionFrame.anchoredPosition = targetPos;
        }
        else
        {
            // 부드러운 이동 연출 (Lerp 사용)
            StopAllCoroutines();
            StartCoroutine(MoveFrameSmooth(targetPos));
        }
    }

    private System.Collections.IEnumerator MoveFrameSmooth(Vector3 target)
    {
        float duration = 0.1f; // 이동 시간
        float elapsed = 0f;
        Vector3 startPos = selectionFrame.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            selectionFrame.anchoredPosition = Vector3.Lerp(startPos, target, elapsed / duration);
            yield return null;
        }
        selectionFrame.anchoredPosition = target;
    }
}
