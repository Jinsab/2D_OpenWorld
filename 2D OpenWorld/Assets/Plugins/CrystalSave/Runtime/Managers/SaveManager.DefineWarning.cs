using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if !MEMORYPACK
namespace Arawn.CrystalSave.Runtime
{
    public enum AuthProvider
    {
        Anonymous,
        Unity,
        UsernamePassword,
        GooglePlay,
        Apple,
        Facebook,
        Steam
    }
}
#endif

#if !MEMORYPACK || !ARAWN_REMEMBERME || !REMEMBERME_AUTHENTICATION_PRESENT || !REMEMBERME_CORESERVICES_PRESENT

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Provides detailed warnings when required scripting define symbols are missing.
    /// </summary>
    internal static class SaveManagerDefineSymbolsWarning
    {
        static readonly string message = BuildMessage();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void WarnMissingDefineSymbols()
        {
#if MEMORYPACK
            var settings = Resources.Load<SaveSettings>("SaveSettings");
            if (settings == null || !settings.enableCloudSave)
            {
                Logger.Log(message, LogLevel.Off);
                return;
            }

            Logger.Log(message, LogLevel.Off);
#endif
        }

        internal static string MissingDefineSymbolsMessage => message;

        /// <summary>
        /// Logs an error explaining why saving is unavailable.
        /// </summary>
        internal static void WarnOnSaveAttempt()
        {
            Logger.Log($"Save operation failed: {message}", LogLevel.Warning);
        }

        /// <summary>
        /// Logs an error explaining why authentication is unavailable.
        /// </summary>
        /// <param name="action">Description of the attempted authentication action.</param>
        internal static void WarnOnAuthAttempt(string action)
        {
            Logger.Log($"{action} failed: {message}", LogLevel.Warning);
        }

        static string BuildMessage()
        {
            var missing = new List<string>();
#if !MEMORYPACK
            missing.Add("MEMORYPACK");
#endif
#if !ARAWN_REMEMBERME
            missing.Add("ARAWN_REMEMBERME");
#endif
#if !REMEMBERME_AUTHENTICATION_PRESENT
            missing.Add("REMEMBERME_AUTHENTICATION_PRESENT");
#endif
#if !REMEMBERME_CORESERVICES_PRESENT
            missing.Add("REMEMBERME_CORESERVICES_PRESENT");
#endif
            return $"Cloud save disabled: required scripting define symbols are missing ({string.Join(", ", missing)}).";
        }
    }

    public partial class SaveManager
    {
        public Task<bool> SignInUsernamePasswordAsync(string email, string password, bool createAccount = true)
        {
            if (createAccount)
                SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("Username/password sign-up");
            else
                SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("Username/password sign-in");

            return Task.FromResult(false);
        }

        public Task<bool> SignInToCloudAsync(AuthProvider? overrideProvider = null)
        {
            SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("Cloud sign-in");
            return Task.FromResult(false);
        }

        public Task<bool> UseEmailLoginAsync(string email, string password)
        {
            SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("Email login");
            return Task.FromResult(false);
        }

        public Task<bool> LinkIdentityAsync(AuthProvider provider)
        {
            SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("Link identity");
            return Task.FromResult(false);
        }

        #if !MEMORYPACK || !ARAWN_REMEMBERME
        public Task<bool> MySqlSignUpAsync(string username, string password)
        {
            SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("MySQL sign-up");
            return Task.FromResult(false);
        }

        public Task<bool> MySqlSignInAsync(string username, string password)
        {
            SaveManagerDefineSymbolsWarning.WarnOnAuthAttempt("MySQL sign-in");
            return Task.FromResult(false);
        }
        #endif
    }
}
#endif

