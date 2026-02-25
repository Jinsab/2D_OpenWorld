#if REMEMBERME_STEAMWORKS_PRESENT && MEMORYPACK && ARAWN_REMEMBERME
using System;
using Steamworks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Utility for obtaining a Steam auth-session ticket.</summary>
    internal static class SteamHelper
    {
        public static string GetSessionTicket()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("Steam not initialized.");
                return null;
            }

            byte[] buffer = new byte[1024];
            uint size;

            // ← Create & pass a SteamNetworkingIdentity so the 4-arg overload matches
            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            HAuthTicket ticket = SteamUser.GetAuthSessionTicket(
                buffer,
                buffer.Length,
                out size,
                ref identity
            );

            if (ticket == HAuthTicket.Invalid || size == 0)
                return null;

            return Convert.ToBase64String(buffer, 0, (int)size);
        }

        public static string GetIdentity() => "steam";
    }
}
#endif
