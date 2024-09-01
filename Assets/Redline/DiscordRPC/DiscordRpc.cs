using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;

namespace Redline.DiscordRPC {
  public abstract class DiscordRpc {
    [MonoPInvokeCallback(typeof (OnReadyInfo))]
    public static void ReadyCallback(ref DiscordUser connectedUser) {
      Callbacks.ReadyCallback(ref connectedUser);
    }

    public delegate void OnReadyInfo(ref DiscordUser connectedUser);

    [MonoPInvokeCallback(typeof (OnDisconnectedInfo))]
    public static void DisconnectedCallback(int errorCode, string message) {
      Callbacks.DisconnectedCallback(errorCode, message);
    }

    public delegate void OnDisconnectedInfo(int errorCode, string message);

    [MonoPInvokeCallback(typeof (OnErrorInfo))]
    public static void ErrorCallback(int errorCode, string message) {
      Callbacks.ErrorCallback(errorCode, message);
    }

    public delegate void OnErrorInfo(int errorCode, string message);

    [MonoPInvokeCallback(typeof (OnJoinInfo))]
    public static void JoinCallback(string secret) {
      Callbacks.JoinCallback(secret);
    }

    public delegate void OnJoinInfo(string secret);

    [MonoPInvokeCallback(typeof (OnSpectateInfo))]
    public static void SpectateCallback(string secret) {
      Callbacks.SpectateCallback(secret);
    }

    public delegate void OnSpectateInfo(string secret);

    [MonoPInvokeCallback(typeof (OnRequestInfo))]
    public static void RequestCallback(ref DiscordUser request) {
      Callbacks.RequestCallback(ref request);
    }

    public delegate void OnRequestInfo(ref DiscordUser request);

    private static EventHandlers Callbacks {
      get;
      set;
    }

    public struct EventHandlers {
      public OnReadyInfo ReadyCallback;
      public OnDisconnectedInfo DisconnectedCallback;
      public OnErrorInfo ErrorCallback;
      public OnJoinInfo JoinCallback;
      public OnSpectateInfo SpectateCallback;
      public OnRequestInfo RequestCallback;
    }

    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct RichPresenceStruct {
      public IntPtr state; /* max 128 bytes */
      public IntPtr details; /* max 128 bytes */
      public long startTimestamp;
      public long endTimestamp;
      public IntPtr largeImageKey; /* max 32 bytes */
      public IntPtr largeImageText; /* max 128 bytes */
      public IntPtr smallImageKey; /* max 32 bytes */
      public IntPtr smallImageText; /* max 128 bytes */
      public IntPtr partyId; /* max 128 bytes */
      public int partySize;
      public int partyMax;
      public IntPtr matchSecret; /* max 128 bytes */
      public IntPtr joinSecret; /* max 128 bytes */
      public IntPtr spectateSecret; /* max 128 bytes */
      public bool instance;
    }

    [Serializable]
    public struct DiscordUser {
      public string userId;
      public string username;
      public string discriminator;
      public string avatar;
    }

    public enum Reply {
      No = 0,
        Yes = 1,
        Ignore = 2
    }

    public static void Initialize(string applicationId, ref EventHandlers handlers, bool autoRegister,
      string optionalSteamId) {
      Callbacks = handlers;

      EventHandlers staticEventHandlers = new EventHandlers();
      staticEventHandlers.ReadyCallback += ReadyCallback;
      staticEventHandlers.DisconnectedCallback += DisconnectedCallback;
      staticEventHandlers.ErrorCallback += ErrorCallback;
      staticEventHandlers.JoinCallback += JoinCallback;
      staticEventHandlers.SpectateCallback += SpectateCallback;
      staticEventHandlers.RequestCallback += RequestCallback;

      InitializeInternal(applicationId, ref staticEventHandlers, autoRegister, optionalSteamId);
    }

    [DllImport("discord-rpc", EntryPoint = "Discord_Initialize", CallingConvention = CallingConvention.Cdecl)]
    private static extern void InitializeInternal(string applicationId, ref EventHandlers handlers, bool autoRegister,
      string optionalSteamId);

    [DllImport("discord-rpc", EntryPoint = "Discord_Shutdown", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Shutdown();

    [DllImport("discord-rpc", EntryPoint = "Discord_RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
    public static extern void RunCallbacks();

    [DllImport("discord-rpc", EntryPoint = "Discord_UpdatePresence", CallingConvention = CallingConvention.Cdecl)]
    private static extern void UpdatePresenceNative(ref RichPresenceStruct presence);

    [DllImport("discord-rpc", EntryPoint = "Discord_ClearPresence", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearPresence();

    [DllImport("discord-rpc", EntryPoint = "Discord_Respond", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Respond(string userId, Reply reply);

    [DllImport("discord-rpc", EntryPoint = "Discord_UpdateHandlers", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateHandlers(ref EventHandlers handlers);

    public static void UpdatePresence(RichPresence presence) {
      var presencestruct = presence.GetStruct();
      UpdatePresenceNative(ref presencestruct);
      presence.FreeMem();
    }

    public class RichPresence {
      private RichPresenceStruct _presence;
      private readonly List < IntPtr > _buffers = new List < IntPtr > (10);

      public string State; /* max 128 bytes */
      public string Details; /* max 128 bytes */
      public long StartTimestamp;
      public long EndTimestamp;
      public string LargeImageKey; /* max 32 bytes */
      public string LargeImageText; /* max 128 bytes */
      public string SmallImageKey; /* max 32 bytes */
      public string SmallImageText; /* max 128 bytes */
      public string PartyId; /* max 128 bytes */
      public int PartySize;
      public int PartyMax;
      public string MatchSecret; /* max 128 bytes */
      public string JoinSecret; /* max 128 bytes */
      public string SpectateSecret; /* max 128 bytes */
      public bool Instance;

      /// <summary>
      /// Get the <see cref="RichPresenceStruct"/> reprensentation of this instance
      /// </summary>
      /// <returns><see cref="RichPresenceStruct"/> reprensentation of this instance</returns>
      internal RichPresenceStruct GetStruct() {
        if (_buffers.Count > 0) {
          FreeMem();
        }

        _presence.state = StrToPtr(State);
        _presence.details = StrToPtr(Details);
        _presence.startTimestamp = StartTimestamp;
        _presence.endTimestamp = EndTimestamp;
        _presence.largeImageKey = StrToPtr(LargeImageKey);
        _presence.largeImageText = StrToPtr(LargeImageText);
        _presence.smallImageKey = StrToPtr(SmallImageKey);
        _presence.smallImageText = StrToPtr(SmallImageText);
        _presence.partyId = StrToPtr(PartyId);
        _presence.partySize = PartySize;
        _presence.partyMax = PartyMax;
        _presence.matchSecret = StrToPtr(MatchSecret);
        _presence.joinSecret = StrToPtr(JoinSecret);
        _presence.spectateSecret = StrToPtr(SpectateSecret);
        _presence.instance = Instance;

        return _presence;
      }

      /// <summary>
      /// Returns a pointer to a representation of the given string with a size of maxbytes
      /// </summary>
      /// <param name="input">String to convert</param>
      /// <returns>Pointer to the UTF-8 representation of <see cref="input"/></returns>
      private IntPtr StrToPtr(string input) {
        if (string.IsNullOrEmpty(input)) return IntPtr.Zero;
        var convbytecnt = Encoding.UTF8.GetByteCount(input);
        var buffer = Marshal.AllocHGlobal(convbytecnt + 1);
        for (int i = 0; i < convbytecnt + 1; i++) {
          Marshal.WriteByte(buffer, i, 0);
        }

        _buffers.Add(buffer);
        Marshal.Copy(Encoding.UTF8.GetBytes(input), 0, buffer, convbytecnt);
        return buffer;
      }

      /// <summary>
      /// Convert string to UTF-8 and add null termination
      /// </summary>
      /// <param name="toconv">string to convert</param>
      /// <returns>UTF-8 representation of <see cref="toconv"/> with added null termination</returns>
      private static string StrToUtf8NullTerm(string toconv) {
        var str = toconv.Trim();
        var bytes = Encoding.Default.GetBytes(str);
        if (bytes.Length > 0 && bytes[bytes.Length - 1] != 0) {
          str += "\0\0";
        }

        return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(str));
      }

      /// <summary>
      /// Free the allocated memory for conversion to <see cref="RichPresenceStruct"/>
      /// </summary>
      internal void FreeMem() {
        for (var i = _buffers.Count - 1; i >= 0; i--) {
          Marshal.FreeHGlobal(_buffers[i]);
          _buffers.RemoveAt(i);
        }
      }
    }
  }
}