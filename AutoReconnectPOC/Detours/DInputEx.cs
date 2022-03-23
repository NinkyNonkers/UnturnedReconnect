using SDG.Framework.Devkit;
using SDG.Unturned;
using UnityEngine;

namespace AutoReconnectPOC.Detours
{
    public static class DInputEx
    {
        public static bool DetourGetKey(KeyCode key)
        {
            return Input.GetKey(key) && Glazier.Get().ShouldGameProcessKeyDown && ARPoCMod.Instance.SendingInputs;
        }
        
        
    }
}