using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.AddressableAssets; // Addressables 필수
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 작업 필수
using UnityEngine.U2D.Animation; // Sprite Library 필수

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.09 오후 14:32
 *  마지막 수정 일자 : 26.03.13 오후 17:58
 *  
 *  [스크립트 목적 및 내용]
 *  1. 유니티 어드레서블 - 에셋 로더
 *    1-1. 유니티 Addressables 시스템을 활용
 *    1-2. 특정 부위의 SpriteLibraryAsset을 String Address 경로로 로드하여 SpriteLibrary에 적용
 *    1-3. 모든 부위도 가능
 *    1-4. 특정 부위의 SpriteLibrary를 String Address 경로로 변환하는 함수도 포함
 *  
 *  [스크립트 작성 도움 출처]
 *  1. https://stackoverflow.com/questions/1621351/convert-dictionary-keyscollection-to-array-of-strings
 *  2. https://learn.microsoft.com/ko-kr/dotnet/fundamentals/code-analysis/quality-rules/ca1864
 *  3. https://developer-talk.tistory.com/693
 *  4. https://docs.unity3d.com/Packages/com.unity.2d.animation@13.0/manual/SLAsset.html
 *  5. https://learn.microsoft.com/ko-kr/dotnet/csharp/programming-guide/classes-and-structs/how-to-initialize-a-dictionary-with-a-collection-initializer
 *  6. https://discussions.unity.com/t/on-async-await-with-awaitables-as-a-coroutine-replacement/1554090
 */

public class AddressableAssetLoader : MonoBehaviour
{
    [Header("# Addressable Asset Loader")]
    public static AddressableAssetLoader Instance;

    [Header("# Caching Asset Data")]
    [SerializedDictionary("string", "SpriteLibrary")]
    public SerializedDictionary<string, SpriteLibraryAsset> CachingAssetData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
        }
        else
        {
            Destroy(gameObject);
        }

        CachingAssetData = new SerializedDictionary<string, SpriteLibraryAsset>();
    }

    // 에셋 로드 요청이 들어오면 딕셔너리에 해당 주소(Key)의 에셋이 있는지 확인
    // 이미 있다면 await 없이 즉시 반환
    // 없다면 Addressables로 로드 시도 후 딕셔너리에 저장하고 반환
    public async Awaitable<SpriteLibraryAsset> TryAssetLoad(string adr)
    {
        if (string.IsNullOrEmpty(adr))
        {
            Log.Asset("Dictionary의 Key 값은 Null이 될 수 없습니다!");
            return null;
        }

        Log.Asset($"{adr} 에셋 로드를 시도합니다.");

        // 내부 캐싱에 해당 주소(Key)가 있는지 확인하기
        if (CachingAssetData.ContainsKey(adr))
        {
            if (CachingAssetData.TryGetValue(adr, out SpriteLibraryAsset cachedLib))
            {
                if (cachedLib != null)
                {
                    // 캐싱된 에셋이 유효하다면 즉시 반환
                    Log.Asset($"{adr} 에셋이 이미 로드되어 있습니다.");

                    return cachedLib;
                }
            }
        }

        // 해당 주소(Key)가 없었으므로 내부 캐싱에 로드를 시도
        Log.Asset($"{adr} 에셋의 정보가 없습니다. 로드를 시도합니다.");
        await LoadAndApplyAsset(adr);

        return CachingAssetData.TryGetValue(adr, out SpriteLibraryAsset loadLib) ? loadLib : null;
    }

    public async Awaitable TryAssetLoad(string adr, SpriteLibrary lib)
    {
        if (string.IsNullOrEmpty(adr))
        {
            Log.Asset("Dictionary의 Key 값은 Null이 될 수 없습니다!");
            return;
        }

        Log.Asset($"{adr} 에셋 로드를 시도합니다.");

        // 내부 캐싱에 해당 주소(Key)가 있는지 확인하기
        if (CachingAssetData.ContainsKey(adr))
        {
            if (CachingAssetData.TryGetValue(adr, out SpriteLibraryAsset cachedLib))
            {
                if (cachedLib != null)
                {
                    // 캐싱된 에셋이 유효하다면 즉시 반환
                    Log.Asset($"{adr} 에셋이 이미 로드되어 있습니다.");

                    lib.spriteLibraryAsset = cachedLib;
                    return;
                }
            }
        }

        // 해당 주소(Key)가 없었으므로 내부 캐싱에 로드를 시도
        Log.Asset($"{adr} 에셋의 정보가 없습니다. 로드를 시도합니다.");
        await LoadAndApplyAsset(adr);

        lib.spriteLibraryAsset = CachingAssetData.TryGetValue(adr, out SpriteLibraryAsset loadLib) ? loadLib : null;
    }

    public async Awaitable<SpriteLibraryAsset[]> TryAssetLoad(string[] adrs)
    {
        if (adrs == null)
            return null;
        else
            if (adrs.Length == 0)
                return null;

        SpriteLibraryAsset[] chacedlibs = new SpriteLibraryAsset[adrs.Length];

        for (int i = 0; i < adrs.Length; i++)
        {
            if (adrs == null)
            {
                Log.Asset("Dictionary의 Key 값은 Null이 될 수 없습니다!");
                return null;
            }
            else
            {
                Log.Asset($"{adrs[i]} 에셋 로드를 시도합니다.");

                // 내부 캐싱에 해당 주소(Key)가 있는지 확인하기
                if (CachingAssetData.ContainsKey(adrs[i]))
                {
                    if (CachingAssetData.TryGetValue(adrs[i], out SpriteLibraryAsset cachedLib))
                    {
                        if (cachedLib != null)
                        {
                            // 캐싱된 에셋이 유효하다면 즉시 반환
                            Log.Asset($"{adrs[i]} 에셋이 이미 로드되어 있습니다.");

                            chacedlibs[i] = cachedLib;
                        }
                    }
                }
                else
                {
                    // 해당 주소(Key)가 없었으므로 내부 캐싱에 로드를 시도
                    Log.Asset($"{adrs[i]} 에셋의 정보가 없습니다. 로드를 시도합니다.");
                    await LoadAndApplyAsset(adrs[i]);

                    chacedlibs[i] = CachingAssetData.TryGetValue(adrs[i], out SpriteLibraryAsset loadLib) ? loadLib : null;
                }
            }
        }

        return chacedlibs;
    }

    public async Awaitable TryAssetLoad(string[] adrs, SpriteLibrary[] libs)
    {
        if (adrs == null)
            return;
        else
            if (adrs.Length == 0)
                return;

        for (int i = 0; i < adrs.Length; i++)
        {
            if (adrs == null)
            {
                Log.Asset("Dictionary의 Key 값은 Null이 될 수 없습니다!");
                return;
            }
            else
            {
                Log.Asset($"{adrs[i]} 에셋 로드를 시도합니다.");

                // 내부 캐싱에 해당 주소(Key)가 있는지 확인하기
                if (CachingAssetData.ContainsKey(adrs[i]))
                {
                    if (CachingAssetData.TryGetValue(adrs[i], out SpriteLibraryAsset cachedLib))
                    {
                        if (cachedLib != null)
                        {
                            // 캐싱된 에셋이 유효하다면 즉시 반환
                            Log.Asset($"{adrs[i]} 에셋이 이미 로드되어 있습니다.");

                            libs[i].spriteLibraryAsset = cachedLib;
                        }
                    }
                }
                else
                {
                    // 해당 주소(Key)가 없었으므로 내부 캐싱에 로드를 시도
                    Log.Asset($"{adrs[i]} 에셋의 정보가 없습니다. 로드를 시도합니다.");
                    await LoadAndApplyAsset(adrs[i]);

                    libs[i].spriteLibraryAsset = CachingAssetData.TryGetValue(adrs[i], out SpriteLibraryAsset loadLib) ? loadLib : null;
                }
            }
        }
    }

    // 특정 부위의 SpriteLibraryAsset을 로드하여 적용하는 함수
    public async Awaitable LoadAndApplyAsset(string assetAddress)
    {
        if (string.IsNullOrEmpty(assetAddress)) return;

        // Addressables를 통해 비동기로 에셋 로드 시도
        AsyncOperationHandle<SpriteLibraryAsset> handle = Addressables.LoadAssetAsync<SpriteLibraryAsset>(assetAddress);

        // 작업 완료까지 대기 (await를 사용하려면 함수에 async 키워드 필요)
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // 성공 로그 출력 및 내부 캐싱을 도입하여 딕셔너리에 값을 넣음
            // 최종적으로는 메모리 해제
            CachingAssetData.TryAdd(assetAddress, handle.Result);

            Log.Asset($"{assetAddress} 로드 및 적용 완료");
        }
        else
        {
            // 실패 시에는 에러 로그 출력
            Log.Error("Asset", $"{assetAddress} 로드 실패!");
        }

        // 성공, 실패 상관 없이 메모리 해제
        Addressables.Release(handle);
    }

    // n개의 SpriteLibraryAsset을 로드하여 적용하는 함수
    public async Awaitable LoadAndApplyAsset(string[] assetAdrs)
    {
        if (assetAdrs == null) return;

        // 1. 모든 핸들을 리스트에 담아 동시에 실행
        var locationsTask = Addressables.LoadResourceLocationsAsync(assetAdrs, Addressables.MergeMode.Union);
        var locations = await locationsTask.Task;

        if (locationsTask.Status == AsyncOperationStatus.Failed)
        {
            Log.Error("Asset", "Addressables Load Resource Locations Failed!");
            return;
        }

        // 2. 모든 에셋을 한 번에 로드 (IList<SpriteLibraryAsset>)
        var loadTask = Addressables.LoadAssetsAsync<SpriteLibraryAsset>(locations, null);
        var loadedAssets = await loadTask.Task;

        // 3. 로드된 결과를 부위별 컴포넌트에 매칭하여 할당
        if (loadTask.Status == AsyncOperationStatus.Succeeded)
        {
            for (int i = 0; i < loadedAssets.Count; i++)
            {
                // 각 부위 안전하게 할당
                if (CachingAssetData.TryAdd(assetAdrs[i], loadedAssets[i]))
                {
                    Log.Asset($"{assetAdrs[i]} 로드 완료");
                }
                else
                {
                    Log.Asset($"{assetAdrs[i]} 가 이미 있습니다!");
                }
            }
        }
        else
        {
            // 실패 시에는 에러 로그 출력 및 메모리 해제
            Log.Error("Asset", "Addressables Load Asset Failed!");
        }

        Addressables.Release(locationsTask);
        Addressables.Release(loadTask);
    }

    //특정 부위의 SpriteLibrary를 String Address 경로로 변환하는 함수
    //public void SaveAssetAddress(string assetAddress, SpriteLibrary library)
    //{
    //    if (library == null || library.spriteLibraryAsset == null) return;

    //    var asset = library.spriteLibraryAsset;

    //    // 모든 로케이터를 순회하며 해당 에셋의 위치 정보를 찾습니다.
    //    foreach (var locator in Addressables.ResourceLocators)
    //    {
    //        // 에셋의 인스턴스 ID나 참조를 통해 위치 정보를 확인합니다.
    //        if (locator.Locate(asset, typeof(SpriteLibraryAsset), out var locations))
    //        {
    //            // 첫 번째 일치하는 주소(Primary Key)를 반환합니다.
    //            Debug.Log(locations[0].PrimaryKey);
    //        }

    //        Addressables.Release(locator);
    //    }

    //    Debug.Log("Address Not Found");
    //}

    // 씬이 종료되거나 캐릭터가 파괴될 때 메모리 해제가 필요할 수 있습니다.
    // Addressables.Release(handle); 호출 필요
}