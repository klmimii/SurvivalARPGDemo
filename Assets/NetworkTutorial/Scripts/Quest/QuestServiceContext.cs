using System;

public static class QuestServiceContext
{
    private static IQuestService networkService;

    public static event Action BindingChanged;

    public static IQuestService Current =>
        networkService ?? GameBootstrap.QuestService;

    public static void BindNetwork(IQuestService service)
    {
        networkService = service;
        BindingChanged?.Invoke();
    }

    public static void UnbindNetwork(IQuestService service)
    {
        if (!ReferenceEquals(networkService, service))
        {
            return;
        }

        networkService = null;
        BindingChanged?.Invoke();
    }
}