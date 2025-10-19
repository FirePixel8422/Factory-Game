using System;
using UnityEngine;

public static class UpdateScheduler
{
    private static Action OnUpdate;
    private static Action OnFixedUpdate;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        UpdateCallbackManager gameManager = new GameObject("GameManager").AddComponent<UpdateCallbackManager>();

        GameObject.DontDestroyOnLoad(gameManager.gameObject);
    }


    /// <summary>
    /// Register a method to call every frame like Update()
    /// </summary>
    public static void RegisterUpdate(Action action)
    {
        OnUpdate += action;
    }
    /// <summary>
    /// Unregister a registerd method for Update()
    /// </summary>
    public static void UnregisterUpdate(Action action)
    {
        OnUpdate -= action;
    }

    /// <summary>
    /// Register a method to call every frame like FixedUpdate()
    /// </summary>
    public static void RegisterFixedUpdate(Action action)
    {
        OnFixedUpdate += action;
    }
    /// <summary>
    /// Unregister a registerd method for FixedUpdate()
    /// </summary>
    public static void UnregisterFixedUpdate(Action action)
    {
        OnFixedUpdate -= action;
    }



    /// <summary>
    /// Handle Update Callbacks and batch them for every script by an event based register system
    /// </summary>
    private class UpdateCallbackManager : MonoBehaviour
    {
        private void Update()
        {
            OnUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            OnFixedUpdate?.Invoke();
        }
    }
}