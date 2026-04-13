using System;
using System.Reflection;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace AURAID.Integration
{
    /// <summary>
    /// Ensures Firebase is initialized and attempts anonymous sign-in when Firebase Auth is present.
    /// Uses reflection for Auth so the project still compiles if Auth SDK is not installed yet.
    /// </summary>
    public class FirebaseAnonymousAuthBootstrap : MonoBehaviour
    {
        [SerializeField] bool logVerbose = true;
        static bool _started;

        void Awake()
        {
            if (_started) return;
            _started = true;
            DontDestroyOnLoad(gameObject);
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(OnDepsReady);
        }

        void OnDepsReady(Task<DependencyStatus> task)
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] Dependency check failed: " + task.Exception);
                return;
            }
            if (task.Result != DependencyStatus.Available)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] Firebase dependencies unavailable: " + task.Result);
                return;
            }

            TryAnonymousSignIn();
        }

        void TryAnonymousSignIn()
        {
            Type authType = Type.GetType("Firebase.Auth.FirebaseAuth, Firebase.Auth");
            if (authType == null)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] Firebase Auth SDK not found. Install FirebaseAuth if rules require request.auth.");
                return;
            }

            object auth = authType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
            if (auth == null)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] FirebaseAuth.DefaultInstance is null.");
                return;
            }

            MethodInfo signInMethod = authType.GetMethod("SignInAnonymouslyAsync", BindingFlags.Public | BindingFlags.Instance);
            if (signInMethod == null)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] SignInAnonymouslyAsync not found.");
                return;
            }

            Task authTask = signInMethod.Invoke(auth, null) as Task;
            if (authTask == null)
            {
                if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] SignInAnonymouslyAsync returned no task.");
                return;
            }

            authTask.ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    if (logVerbose) Debug.LogWarning("[FirebaseAuthBootstrap] Anonymous sign-in failed: " + t.Exception);
                }
                else if (logVerbose)
                {
                    Debug.Log("[FirebaseAuthBootstrap] Anonymous sign-in succeeded.");
                }
            });
        }
    }
}
