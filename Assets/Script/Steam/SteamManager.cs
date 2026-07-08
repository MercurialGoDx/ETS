// The SteamManager is designed to work with Steamworks.NET
// This file is based on the canonical SteamManager.cs distributed by Steamworks.NET
// (rlabrecque, public-domain / "do whatever you want" license). It initialises the Steam API,
// pumps callbacks each frame and shuts it down cleanly. Everything is compiled out on platforms
// where Steamworks is unavailable (DISABLESTEAMWORKS), so non-Steam builds keep working.

#if !DISABLESTEAMWORKS
using System.Collections;
using UnityEngine;
using Steamworks;
#endif

[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour
{
#if !DISABLESTEAMWORKS
    protected static bool s_EverInitialized = false;

    protected static SteamManager s_instance;
    protected static SteamManager Instance
    {
        get
        {
            if (s_instance == null)
                return new GameObject("SteamManager").AddComponent<SteamManager>();
            return s_instance;
        }
    }

    protected bool m_bInitialized = false;
    public static bool Initialized
    {
        get { return Instance.m_bInitialized; }
    }

    protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

    [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
    protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
    {
        Debug.LogWarning(pchDebugText);
    }

#if UNITY_2019_3_OR_NEWER
    // In case of disabled Domain Reload, reset static members before entering Play Mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitOnPlayMode()
    {
        s_EverInitialized = false;
        s_instance = null;
    }
#endif

    protected virtual void Awake()
    {
        // Only one instance of SteamManager at a time!
        if (s_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        if (s_EverInitialized)
        {
            // Steam API can only be initialised once per process.
            throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");
        }

        // Keep the SteamManager alive across scene loads.
        DontDestroyOnLoad(gameObject);

        if (!Packsize.Test())
            Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);

        if (!DllCheck.Test())
            Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);

        try
        {
            // If Steam is not running or the game wasn't started through Steam,
            // SteamAPI_RestartAppIfNecessary starts Steam and relaunches through it if steam_appid.txt exists.
            if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
            {
                Application.Quit();
                return;
            }
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + e, this);
            Application.Quit();
            return;
        }

        m_bInitialized = SteamAPI.Init();
        if (!m_bInitialized)
        {
            Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", this);
            return;
        }

        s_EverInitialized = true;
    }

    protected virtual void OnEnable()
    {
        if (s_instance == null)
            s_instance = this;

        if (!m_bInitialized)
            return;

        if (m_SteamAPIWarningMessageHook == null)
        {
            m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
            SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
        }
    }

    protected virtual void OnDestroy()
    {
        if (s_instance != this)
            return;

        s_instance = null;

        if (!m_bInitialized)
            return;

        SteamAPI.Shutdown();
    }

    protected virtual void Update()
    {
        if (!m_bInitialized)
            return;

        // Run Steam client callbacks.
        SteamAPI.RunCallbacks();
    }
#else
    public static bool Initialized
    {
        get { return false; }
    }
#endif
}
