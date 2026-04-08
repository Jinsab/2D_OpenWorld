using TMPro;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.07 오후 20:15
 *  마지막 수정 일자 : 26.04.08 오후 19:49
 *  
 *  [스크립트 목적 및 내용]
 *  1. 스탯 UI 프리팹
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class StatRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statText;
    [SerializeField] private string label;
    [SerializeField] private string value;

    public bool IsZeroValue => value == "0%" || value == "0";
    public void SetView(bool active) => this.gameObject.SetActive(active);
    public void SetLabel(string text) => label = text;
    public void SetValue(string text) => value = text;
    public void SetStatText() => statText.text = $"{label} {value}";
}
