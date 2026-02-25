#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
#if REMEMBERME_CLOUDSAVE_PRESENT
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif
#endif

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>
        /// Handles cloud related authentication and slot refresh logic.
        /// </summary>
        public class CloudSaveService
        {
                readonly SaveManager manager;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                string cachedEmail;
                string cachedPassword;
                bool   cachedCreateAccount;
#endif
                public CloudSaveService(SaveManager manager)
                {
                        this.manager = manager;
                }

                /* ───────────── Supabase Integration ───────────── */
                async Task<List<SaveSlot>> SafeListSlotsAsync(SupabaseSaveSystem s)
                {
                        var list = await s.ListRemoteSlotsAsync();
                        // Note: 0 slots is a valid result for new accounts, don't retry
                        return list;
                }
                async Task<List<SaveSlot>> SafeListSlotsAsync(FirebaseSaveSystem s)
                {
                        var list = await s.ListRemoteSlotsAsync();
                        // Note: 0 slots is a valid result for new accounts, don't retry
                        return list;
                }


                async Task<List<SaveSlot>> SafeListSlotsAsync(SaveSystem s)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log("[CloudSaveService] WebGL: SafeListSlotsAsync(SaveSystem) started");
#endif
                        var list = await s.ListRemoteSlotsAsync();
#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log($"[CloudSaveService] WebGL: ListRemoteSlotsAsync call returned {list?.Count ?? 0} slots");
#endif
                        // Note: 0 slots is a valid result for new accounts, don't retry
                        return list;
                }

                async Task<List<SaveSlot>> SafeListSlotsAsync(MySqlSaveSystem s)
                {
                        var list = await s.ListRemoteSlotsAsync();
                        // Note: 0 slots is a valid result for new accounts, don't retry
                        return list;
                }

                public async Task RefreshRemoteSlotsAsync()
                {
                        List<SaveSlot> remote = null;

#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log($"[CloudSaveService] WebGL: RefreshRemoteSlotsAsync started, backend: {manager.SaveSettings.backend}");
                        Debug.Log($"[CloudSaveService] WebGL: Current local slots count: {manager.SaveSlotsInternal?.Count ?? 0}");
                        if (manager.SaveSlotsInternal != null)
                        {
                            for (int i = 0; i < manager.SaveSlotsInternal.Count; i++)
                            {
                                var slot = manager.SaveSlotsInternal[i];
                                Debug.Log($"[CloudSaveService] WebGL: Local slot {i}: SlotNumber={slot.SlotNumber}, LastSaved={slot.LastSaved}");
                            }
                        }
#endif

                        if (manager.SaveSettings.backend == SaveBackend.Supabase &&
                            manager.SaveSystemInternal is SupabaseSaveSystem supabase)
                        {
                                remote = await SafeListSlotsAsync(supabase);
                        }
                        else if (manager.SaveSettings.backend == SaveBackend.Firebase &&
                                 manager.SaveSystemInternal is FirebaseSaveSystem firebase)
                        {
                                remote = await SafeListSlotsAsync(firebase);
                        }
                        else if (manager.SaveSettings.backend == SaveBackend.UnityCloudSave &&
                                 manager.SaveSystemInternal is SaveSystem saveSystem)
                        {
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log("[CloudSaveService] WebGL: Calling SafeListSlotsAsync for Unity Cloud Save");
                                Debug.Log($"[CloudSaveService] WebGL: SaveSystemInternal type: {manager.SaveSystemInternal?.GetType().Name}");
                                Debug.Log($"[CloudSaveService] WebGL: saveSystem variable: {saveSystem?.GetType().Name}");
#endif
                                remote = await SafeListSlotsAsync(saveSystem);
#if UNITY_WEBGL && !UNITY_EDITOR
                                Debug.Log($"[CloudSaveService] WebGL: SafeListSlotsAsync returned {remote?.Count ?? 0} slots");
#endif
                        }
                         else if (manager.SaveSettings.backend == SaveBackend.MySQL &&
                                  manager.SaveSystemInternal is MySqlSaveSystem mysql)
                         {
                                 remote = await SafeListSlotsAsync(mysql);
                         }
                         else
                         {
#if UNITY_WEBGL && !UNITY_EDITOR
                                 Debug.LogWarning($"[CloudSaveService] WebGL: No matching backend found! Backend: {manager.SaveSettings.backend}, SaveSystemInternal type: {manager.SaveSystemInternal?.GetType().Name}");
#endif
                         }

            remote ??= new List<SaveSlot>();

            // Remove stale local metadata when cloud saves were deleted remotely
            if (manager.SaveSettings.enableCloudSave && !manager.SaveSettings.keepLocalMirror)
            {
                var remoteNums = new HashSet<int>(remote.Select(r => r.SlotNumber));
                var stale = manager.SaveSlotsInternal
                    .Where(s => s.LastSaved > DateTime.MinValue && 
                               !remoteNums.Contains(s.SlotNumber) &&
                               // Don't remove recently saved slots (within last 10 seconds) to avoid race conditions
                               (DateTime.UtcNow - s.LastSaved).TotalSeconds > 10)
                    .ToList();
                
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[CloudSaveService] WebGL: Checking for stale slots - remoteNums: [{string.Join(", ", remoteNums)}]");
                foreach (var s in manager.SaveSlotsInternal)
                {
                    var age = (DateTime.UtcNow - s.LastSaved).TotalSeconds;
                    Debug.Log($"[CloudSaveService] WebGL: Local slot {s.SlotNumber}: LastSaved={s.LastSaved}, age={age:F1}s, inRemote={remoteNums.Contains(s.SlotNumber)}");
                }
                Debug.Log($"[CloudSaveService] WebGL: Found {stale.Count} stale slots to remove");
#endif
                
                foreach (var s in stale)
                {
                    if (manager.SaveSettings.backend == SaveBackend.UnityCloudSave &&
                        manager.SaveSystemInternal is SaveSystem sys)
                        await sys.DeleteLocalSlotAsync(s);
                    else if (manager.SaveSettings.backend == SaveBackend.Supabase &&
                             manager.SaveSystemInternal is SupabaseSaveSystem sup)
                        await sup.DeleteLocalSlotAsync(s);
                    else if (manager.SaveSettings.backend == SaveBackend.Firebase &&
                             manager.SaveSystemInternal is FirebaseSaveSystem fb)
                        await fb.DeleteLocalSlotAsync(s);
                    else if (manager.SaveSettings.backend == SaveBackend.MySQL &&
                               manager.SaveSystemInternal is MySqlSaveSystem my)
                          await my.DeleteLocalSlotAsync(s);

                    s.SlotName = $"Slot {s.SlotNumber}";
                    s.LastSaved = DateTime.MinValue;
                    s.ScreenshotFileName = string.Empty;
                    s.LastActiveScene = string.Empty;
                }
            }

                        // Merge local and remote carefully to avoid overwriting fresh local names with stale remote metadata
                        var localMap = manager.SaveSlotsInternal.ToDictionary(s => s.SlotNumber, s => s);
                        foreach (var r in remote)
                        {
                                if (localMap.TryGetValue(r.SlotNumber, out var l))
                                {
                                        // Prefer the newer entry by LastSaved, but don't lose a non-default name/screenshot/metadata
                                        var newer = r.LastSaved >= l.LastSaved ? r : l;
                                        var other = ReferenceEquals(newer, r) ? l : r;

                                        bool IsDefaultName(SaveSlot s) => string.IsNullOrWhiteSpace(s.SlotName) || s.SlotName == $"Slot {s.SlotNumber}";

                                        if (IsDefaultName(newer) && !IsDefaultName(other))
                                                newer.SlotName = other.SlotName;

                                        if (string.IsNullOrEmpty(newer.ScreenshotFileName) && !string.IsNullOrEmpty(other.ScreenshotFileName))
                                                newer.ScreenshotFileName = other.ScreenshotFileName;

                                        // Merge simple custom metadata if present (prefer newer but fill gaps)
                                        if ((newer.CustomMetadata == null || newer.CustomMetadata.Count == 0) && other.CustomMetadata != null && other.CustomMetadata.Count > 0)
                                                newer.CustomMetadata = new Dictionary<string, string>(other.CustomMetadata);

                                        localMap[r.SlotNumber] = newer;
                                }
                                else
                                {
                                        localMap[r.SlotNumber] = r;
                                }
                        }

                        var merged = localMap.Values.OrderBy(s => s.SlotNumber).ToList();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[CloudSaveService] WebGL: Merging slots - local: {manager.SaveSlotsInternal?.Count ?? 0}, remote: {remote?.Count ?? 0}, merged: {merged.Count}");
            for (int i = 0; i < merged.Count; i++)
            {
                var slot = merged[i];
                Debug.Log($"[CloudSaveService] WebGL: Merged slot {i}: SlotNumber={slot.SlotNumber}, LastSaved={slot.LastSaved}");
            }
#endif

            manager.SaveSlotsInternal.Clear();
            manager.SaveSlotsInternal.AddRange(merged);

            if (manager.SlotManager != null)
            {
                manager.SlotManager.Slots.Clear();
                manager.SlotManager.Slots.AddRange(merged);
            }

                        manager.NotifySaveSlotsUpdated();
                        Logger.Log("SaveManager: remote slots merged.",
                                    LogLevel.Info);

#if UNITY_WEBGL && !UNITY_EDITOR
                        Debug.Log("[CloudSaveService] WebGL: RefreshRemoteSlotsAsync completed successfully");
#endif
                }

                public async void OnSupabaseLoggedIn(string uid)
                {
                        Debug.Log($"Supabase logged in with UID: {uid}");
                        if (manager.SaveSettings.backend != SaveBackend.Supabase)
                                return;

                        if (manager.SaveSettings.userFolderStrategy == UserFolderStrategy.Custom)
                                manager.IsSupabaseCustomLoggedInInternal = true;

                        await manager.InitializeSaveSlotsAsync(manager.SaveSettings.numberOfSaveSlots);
                        await RefreshRemoteSlotsAsync();
                        manager.NotifySaveSlotsUpdated();
                }

                public void LogoutFromSupabase()
                {
                        if (!manager.SaveSettings.enableCloudSave || manager.SaveSettings.backend != SaveBackend.Supabase)
                                return;

                        manager.IsSupabaseCustomLoggedInInternal = false;

                        if (manager.SaveSettings.userFolderStrategy == UserFolderStrategy.Custom &&
                            manager.SaveSettings.customUserFolderResolver is CustomFolderAuthResolver cr)
                        {
                                cr.ClearCredentials();
                        }

                        Logger.Log("Supabase: logged out.", LogCategory.CloudSave, LogLevel.Info);
                }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                /* ───────────── Unity Cloud Sign-In ───────────── */
               public Task<bool> SignInUsernamePasswordAsync(string email, string password, bool createAccount = true)
                {
                        cachedEmail         = email;
                        cachedPassword      = password;
                        cachedCreateAccount = createAccount;
                        return SignInToCloudAsync(AuthProvider.UsernamePassword);
                }

               public async Task OnCloudSignedIn()
               {
#if REMEMBERME_CLOUDSAVE_PRESENT
                       // Unity Cloud Save initializes automatically when UnityServices.InitializeAsync completes.
                       // Older versions exposed InitAsync, which has since been removed.
#endif
                       await manager.InitializeSaveSlotsAsync(manager.SaveSettings.numberOfSaveSlots);
                       await RefreshRemoteSlotsAsync();
                       manager.NotifySaveSlotsUpdated();
               }

                public async Task<bool> SignInToCloudAsync(AuthProvider? overrideProvider = null)
                {
                        if (!manager.SaveSettings.enableCloudSave)
                                return false;

                        if (AuthenticationService.Instance.IsSignedIn)
                                return true;

                        AuthProvider original = manager.SaveSettings.defaultAuthProvider;
                        if (overrideProvider.HasValue)
                                manager.SaveSettings.defaultAuthProvider = overrideProvider.Value;
                        try
                        {
                                await InitializeCloudServicesAsync();
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Cloud sign-in failed: {ex.Message}\n{ex.StackTrace}");
                        }
                        finally
                        {
                                manager.SaveSettings.defaultAuthProvider = original;
                        }
                        return AuthenticationService.Instance.IsSignedIn;
                }

                public async Task<bool> UseEmailLoginAsync(string email, string password)
                {
                        cachedEmail = email;
                        cachedPassword = password;
                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                                await AuthenticationService.Instance.AddUsernamePasswordAsync(email, password);
                        }
                        else
                        {
                                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);
                        }
                        return true;
                }

                public async Task<bool> LinkIdentityAsync(AuthProvider provider)
                {
                        if (!AuthenticationService.Instance.IsSignedIn)
                        {
                                Logger.Log("LinkIdentityAsync: player is not signed-in yet.", LogCategory.CloudSave, LogLevel.Warning);
                                return false;
                        }

                        try
                        {
                                switch (provider)
                                {
                                        case AuthProvider.UsernamePassword:
                                                {
                                                        string email = cachedEmail;
                                                        string password = cachedPassword;
                                                        await UseEmailLoginAsync(email, password);
                                                        break;
                                                }
                                        case AuthProvider.Unity:
                                                {
                                                        var acct = PlayerAccountService.Instance;
                                                        if (!acct.IsSignedIn)
                                                                await acct.StartSignInAsync();
                                                        await AuthenticationService.Instance.LinkWithUnityAsync(acct.AccessToken);
                                                        break;
                                                }
#if REMEMBERME_GOOGLEPLAY_PRESENT
                                        case AuthProvider.GooglePlay:
                                                {
                                                        string code = await GooglePlayHelper.GetServerAuthCodeAsync();
                                                        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code);
                                                        break;
                                                }
#endif
#if REMEMBERME_APPLE_SIGNIN_PRESENT
                                        case AuthProvider.Apple:
                                                {
                                                        string jwt = await AppleSignInHelper.GetJwtAsync();
                                                        await AuthenticationService.Instance.LinkWithAppleAsync(jwt);
                                                        break;
                                                }
#endif
#if REMEMBERME_FACEBOOK_SDK_PRESENT
                                        case AuthProvider.Facebook:
                                                {
                                                        string fb = await FacebookSDK.GetAccessTokenAsync();
                                                        await AuthenticationService.Instance.LinkWithFacebookAsync(fb);
                                                        break;
                                                }
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
                                        case AuthProvider.Steam:
                                                {
                                                        string ticket = SteamHelper.GetSessionTicket();
                                                        string identity = SteamHelper.GetIdentity() ?? "steam";
                                                        await AuthenticationService.Instance.LinkWithSteamAsync(ticket, identity);
                                                        break;
                                                }
#endif
                                        case AuthProvider.Anonymous:
                                        default:
                                                Logger.Log("LinkIdentityAsync: provider 'Anonymous' does not add a new identity.", LogCategory.CloudSave, LogLevel.Info);
                                                return false;
                                }

                                Logger.Log($"Linked {provider} identity to player {AuthenticationService.Instance.PlayerId}.", LogCategory.CloudSave, LogLevel.Info);
                                return true;
                        }
                        catch (AuthenticationException ex)
                        {
                                Logger.Log($"LinkIdentityAsync failed: {ex.Message}", LogCategory.CloudSave, LogLevel.Error);
                                return false;
                        }
                }

                public async Task InitializeCloudServicesAsync()
                {
                        try
                        {
                                await UnityServices.InitializeAsync();
                                Logger.Log("Unity Services initialised.", LogCategory.CloudSave, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Unity Services initialization failed: {ex.Message}\n{ex.StackTrace}");
                                return;
                        }

                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                                await OnCloudSignedIn();
                                return;
                        }

                        try
                        {
                                switch (manager.SaveSettings.defaultAuthProvider)
                                {
                                        case AuthProvider.UsernamePassword:
                                                {
                                                       string email    = cachedEmail;
                                                       string password = cachedPassword;
                                                       bool create     = cachedCreateAccount;

                                                       if (create)
                                                               await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email, password);
                                                       else
                                                               await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);

                                                       break;
                                               }
                                        case AuthProvider.Unity:
                                                {
                                                        var playerSvc = PlayerAccountService.Instance;
                                                        if (!playerSvc.IsSignedIn)
                                                        {
                                                                var tcs = new TaskCompletionSource<bool>();
                                                                void OnSignedIn()
                                                                {
                                                                        playerSvc.SignedIn -= OnSignedIn;
                                                                        tcs.TrySetResult(true);
                                                                }
                                                                playerSvc.SignedIn += OnSignedIn;
                                                                await playerSvc.StartSignInAsync();
                                                                await tcs.Task;
                                                        }
                                                        await AuthenticationService.Instance.SignInWithUnityAsync(
                                                                playerSvc.AccessToken,
                                                                new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#if REMEMBERME_GOOGLEPLAY_PRESENT
                                        case AuthProvider.GooglePlay:
                                                {
                                                        string authCode = await GooglePlayHelper.GetServerAuthCodeAsync();
                                                        await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(
                                                                authCode, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_APPLE_SIGNIN_PRESENT
                                        case AuthProvider.Apple:
                                                {
                                                        string jwt = await AppleSignInHelper.GetJwtAsync();
                                                        await AuthenticationService.Instance.SignInWithAppleAsync(
                                                                jwt, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_FACEBOOK_SDK_PRESENT
                                        case AuthProvider.Facebook:
                                                {
                                                        string fbToken = await FacebookSDK.GetAccessTokenAsync();
                                                        await AuthenticationService.Instance.SignInWithFacebookAsync(
                                                                fbToken, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
                                        case AuthProvider.Steam:
                                                {
                                                        string ticket = SteamHelper.GetSessionTicket();
                                                        string identity = SteamHelper.GetIdentity() ?? "steam";
                                                        await AuthenticationService.Instance.SignInWithSteamAsync(
                                                                ticket, identity, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
                                        case AuthProvider.Anonymous:
                                        default:
                                                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                                                break;
                                }

                                Logger.Log($"Player signed in with {manager.SaveSettings.defaultAuthProvider}.", LogCategory.CloudSave, LogLevel.Info);
                                await OnCloudSignedIn();
                        }
                        catch (RequestFailedException ex)
                        {
                                Debug.LogError($"Authentication failed ({ex.ErrorCode}): {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Authentication failed: {ex.Message}\n{ex.StackTrace}");
                        }
                }
#endif // Authentication
        }
}
#endif
