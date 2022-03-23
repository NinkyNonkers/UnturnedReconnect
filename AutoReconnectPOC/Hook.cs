using UnityEngine;

namespace AutoReconnectPOC
{
    public static class Hook
    {
        private static bool _hooked;
        private static ARPoCMod _instance;
        private static GameObject _object;
        
        public static void Load()
        {
            if (_hooked)
                return;
            _object = new GameObject();
            _instance = _object.AddComponent<ARPoCMod>();
            _instance.LoadDetours();
            _hooked = true;
        }

        public static void Unload()
        {
            _instance.Unload();
            Object.Destroy(_object);
            _hooked = false;
        }
        
        
    }
}