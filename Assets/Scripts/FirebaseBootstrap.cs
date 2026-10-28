using System;
using System.Threading.Tasks;
using Firebase;

/// <summary>
/// Coordinates Firebase's one-at-a-time dependency check for every Firebase
/// service in the game. Calling FirebaseApp.CheckAndFixDependenciesAsync from
/// two startup components concurrently throws and leaves the later service
/// uninitialized.
/// </summary>
public static class FirebaseBootstrap
{
    private static Task<DependencyStatus> dependencyTask;

    public static Task<DependencyStatus> CheckAndFixDependenciesAsync()
    {
        if (dependencyTask != null) return dependencyTask;

        try
        {
            dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        }
        catch (Exception exception)
        {
            TaskCompletionSource<DependencyStatus> completion = new TaskCompletionSource<DependencyStatus>();
            completion.SetException(exception);
            dependencyTask = completion.Task;
        }

        return dependencyTask;
    }
}
