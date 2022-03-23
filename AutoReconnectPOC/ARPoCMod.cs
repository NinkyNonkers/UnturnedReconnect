using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AutoReconnectPOC.Detours;
using BattlEye;
using SDG.NetTransport;
using SDG.NetTransport.SteamNetworking;
using SDG.NetTransport.SteamNetworkingSockets;
using SDG.NetTransport.SystemSockets;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace AutoReconnectPOC
{
    public class ARPoCMod : MonoBehaviour, IDisposable
    {
        
        //no dependency injection stop talking trojaner
        public static ARPoCMod Instance { get; private set; }


        private BEClient.BECL_BE_DATA _data;
        private IntPtr _beNativeHandle;
        
        //TODO: add change on user manual disconnect
        public bool Connected { get; private set; }
        public bool SendingInputs { get; private set; }



        private static readonly MethodInfo CloseTicketInfo;
        private static readonly MethodInfo ResetChannelsInfo;
        private static readonly FieldInfo LastPingInfo;
        private static readonly FieldInfo ClientTransportInfo;
        private static readonly FieldInfo PingsInfo;
        private static readonly FieldInfo IsDismissedInfo;
        
        public float TimeSinceLastPing
        {
	        get => (float) LastPingInfo.GetValue(null);
	        set => LastPingInfo.SetValue(null, value);
        }

        public IClientTransport Connection
        {
	        get => (IClientTransport) ClientTransportInfo.GetValue(null);
	        set => ClientTransportInfo.SetValue(null, value);
        }
        
        public float[] Pings
        {
	        get => (float[]) PingsInfo.GetValue(null);
	        set => PingsInfo.SetValue(null, value);
        }

        private bool _reconnected;

        public ARPoCMod()
        {
            Instance = this;
        }

        static ARPoCMod()
        {
            CloseTicketInfo = typeof(Provider).GetMethod("closeTicket", BindingFlags.Static | BindingFlags.NonPublic);
            ResetChannelsInfo = typeof(Provider).GetMethod("resetChannels", BindingFlags.Static | BindingFlags.NonPublic);
            ClientTransportInfo =
	            typeof(Provider).GetField("clientTransport", BindingFlags.Static | BindingFlags.NonPublic);
            LastPingInfo = typeof(Provider).GetField("timeSinceLastPing", BindingFlags.Static | BindingFlags.NonPublic);
            PingsInfo = typeof(Provider).GetField("pings", BindingFlags.Static | BindingFlags.NonPublic);
            IsDismissedInfo = typeof(Provider).GetField("isDismissed", BindingFlags.Static | BindingFlags.NonPublic);
        }

        private void OnClientConnected()
        {
	        if (!Connected)
	        {
		        Connected = true;
		        StartCoroutine(Reconnect());
		        return;
	        }
	        _reconnected = true;
        }

        private RedirectCallsState _inputDetour;

        public void LoadDetours()
        {
	        Provider.onClientConnected += OnClientConnected;
	        _inputDetour = RedirectionHelper.RedirectCalls(typeof(InputEx).GetMethod("GetKey", BindingFlags.Public | BindingFlags.Static), 
		        typeof(DInputEx).GetMethod("DetourGetKey", BindingFlags.Public | BindingFlags.Static));
        }

        public void Unload()
        {
	        Provider.onClientConnected -= OnClientConnected;
	        RedirectionHelper.RevertRedirect(typeof(InputEx).GetMethod("GetKey", BindingFlags.Public | BindingFlags.Static), _inputDetour);
        }


        private IEnumerator Reconnect()
        {
	        SendingInputs = true;
            ulong serverIp = Provider.ip;
            int port = Provider.port;
            _data = (BEClient.BECL_BE_DATA) typeof(Provider).GetField("battlEyeClientRunData", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            // ReSharper disable once PossibleNullReferenceException
            _beNativeHandle = (IntPtr) typeof(Provider).GetField("battlEyeClientHandle", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            while (Connected)
            {
                yield return new WaitForSeconds(19 * 60); //modify
                
                StopInputs();
                Disconnect();
                Connect(serverIp, port);
                
                while (!_reconnected)
	                yield return new WaitForSeconds(1); //also modify
                
                _reconnected = false;
                ContinueInputs();
            }
        }

        private void ContinueInputs()
        {
	        SendingInputs = true;
	        IsDismissedInfo.SetValue(Player.player.input, false);
	        Cursor.visible = false;
        }

        private void Connect(ulong ip, int port)
        { 
	        ResetChannelsInfo.Invoke(null, null);
           Lobbies.LinkLobby((uint) ip, (ushort) port);
           TimeSinceLastPing = Time.realtimeSinceStartup;
           Pings = new float[4];
           //Provider.lag((float)info.ping / 1000f);
           //Provider.isLoadingUGC = true;
           
           List<SteamItemInstanceID_t> list = new List<SteamItemInstanceID_t>();
           if (Characters.active.packageShirt != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageShirt);
           if (Characters.active.packagePants != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packagePants);
           if (Characters.active.packageHat != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageHat);
           if (Characters.active.packageBackpack != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageBackpack);
           if (Characters.active.packageVest != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageVest);
           if (Characters.active.packageMask != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageMask);
           if (Characters.active.packageGlasses != 0UL)
	           list.Add((SteamItemInstanceID_t)Characters.active.packageGlasses);
           
           for (int i = 0; i < Characters.packageSkins.Count; i++)
           {
	           ulong num = Characters.packageSkins[i];
	           if (num != 0UL)
		           list.Add((SteamItemInstanceID_t)num);
           }
           
           if (list.Count > 0)
	           SteamInventory.GetItemsByID(out Provider.provider.economyService.wearingResult, list.ToArray(), (uint)list.Count);
           
           Connection = CreateTransport(Provider.currentServerInfo.networkTransport);
           UnturnedLog.info("Initializing {0}", Connection.GetType().Name);
           Connection.Initialize(OnClientTransportReady, OnClientTransportFailure);
        }

        private static IClientTransport CreateTransport(string str)
        {
	        if (string.Equals(str, "sys", StringComparison.OrdinalIgnoreCase))
		        return new ClientTransport_SystemSockets();
	        if (string.Equals(str, "sns", StringComparison.OrdinalIgnoreCase))
		        return new ClientTransport_SteamNetworkingSockets();
	        if (string.Equals(str, "def", StringComparison.OrdinalIgnoreCase))
		        return new ClientTransport_SteamNetworking();
	        UnturnedLog.warn("Unknown net transport tag \"{0}\", using default", str);
	        return new ClientTransport_SteamNetworkingSockets();
        }

        private void Disconnect()
        {
            if (_beNativeHandle != IntPtr.Zero)
            {
                if (_data != null)
                {
                    UnturnedLog.info("Shutting down BattlEye client");
                    bool success = _data.pfnExit();
                    UnturnedLog.info("BattlEye client shutdown result: {0}", success);
                }
                BEClient.FreeLibrary(_beNativeHandle);
                //Provider.battlEyeClientHandle = IntPtr.Zero; substitute if necessary
            }
            Connection.TearDown();
            Lobbies.leaveLobby();
            CloseTicketInfo.Invoke(null, null);
        }

        private void StopInputs()
        {
	        VehicleManager.exitVehicle();
	        IsDismissedInfo.SetValue(Player.player.input, true);
	        Cursor.visible = true;
	        Cursor.lockState = CursorLockMode.None;
	        SendingInputs = false;
        }
        
        
        private static void OnClientTransportReady()
        {
	        //prevents loading workshop, level and map
        }
        
        private void OnClientTransportFailure(string message)
        {
	        Provider._connectionFailureInfo = ESteamConnectionFailureInfo.CUSTOM;
	        UnturnedLog.info("Client transport failure: {0}", message);
	        Provider.disconnect();
	        Connected = false;
        }

        public void Dispose()
        {
	        Unload();
        }
    }
}