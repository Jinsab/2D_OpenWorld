using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.27 오후 16:14
 *  마지막 수정 일자 : 26.03.27 오후 16:47
 *  
 *  [스크립트 목적 및 내용]
 *  1. 에디터 툴 기능
 *    1-1. SoundID를 하드코딩(문자열)하지 않고 자동 생성된 Enum이나 상수를 사용하는 방식
 *    1-2. SoundDatabase에 등록된 SoundID 리스트를 읽어와서 자동으로 C# 스크립트 파일(.cs)을 생성해주는 에디터 툴 기능
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class SoundIdentifierGenerator
{
    [MenuItem("Tools/Generate Sound Enums")]
    public static void Generate()
    {
        // 1. 데이터베이스 로드 (경로에 맞게 수정)
        SoundDatabase db = AssetDatabase.LoadAssetAtPath<SoundDatabase>("Assets/03_Data/04_Sound/SoundDatabase.asset");
        if (db == null) { Debug.LogError("SoundDatabase를 찾을 수 없습니다!"); return; }

        string filePath = Path.Combine(Application.dataPath, "02_Scripts/10_Audio/SND.cs");
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// 이 파일은 자동 생성되었습니다. 수정하지 마세요!");
        sb.AppendLine("public enum SND");
        sb.AppendLine("{");
        foreach (var entry in db.soundEntries)
        {
            if (!string.IsNullOrEmpty(entry.soundID))
                sb.AppendLine($"    {entry.soundID},");
        }
        sb.AppendLine("    None");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("SND Enum 생성 완료!");
    }
}
