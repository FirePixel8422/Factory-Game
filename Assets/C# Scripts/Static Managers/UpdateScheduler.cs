using System;
using System.Collections;
using UnityEngine;

public static class UpdateScheduler
{
    private static event Action OnUpdate;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        GameManager.Instance.StartCoroutine(UpdateLoop());
    }


    public static void Register(Action action)
    {
        OnUpdate += action;
    }

    public static void Unregister(Action action)
    {
        OnUpdate -= action;
    }


    private static IEnumerator UpdateLoop()
    {
        while (true)
        {
            yield return null;

            OnUpdate?.Invoke();
        }
    }
}