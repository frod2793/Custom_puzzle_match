using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Data
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        private static readonly Object s_lock = new Object();
        
        private static T s_instance;
        
        private static bool s_isQuitting = false;

        public static T Instance
        {
            get
            {
                if (s_isQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again - returning null.");
                    return null;
                }

                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = FindAnyObjectByType<T>();

                        if (s_instance == null)
                        {
                            GameObject obj = new GameObject();
                            obj.name = typeof(T).Name;
                            s_instance = obj.AddComponent<T>();
                        }
                    }
                }
                return s_instance;
            }
        }

        protected virtual void Awake()
        {
            if (s_instance == null)
            {
                if (s_instance == null)
                {
                    s_instance = this as T;
                    DontDestroyOnLoad(gameObject);
                }
                else if (s_instance != this)
                {
                    Destroy(gameObject);
                }
            }
        }

        protected void OnApplicationQuit()
        {
            s_isQuitting = true;
        }
    }
}