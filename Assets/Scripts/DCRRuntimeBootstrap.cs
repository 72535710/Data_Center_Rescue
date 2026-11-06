using UnityEngine;
using UnityEngine.SceneManagement;

namespace DataCenterRescue
{
    /// <summary>
    /// Installs the game systems only when the playable data-centre scene is loaded.
    /// This avoids hand-editing scene YAML and keeps the original backup scene untouched.
    /// </summary>
    public static class DCRRuntimeBootstrap
    {
        private static bool listening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!listening)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                listening = true;
            }

            TryInstall();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstall();
        }

        private static void TryInstall()
        {
            // Keep the original blank DataCenter scene available as an untouched backup.
            if (SceneManager.GetActiveScene().name != "DataCenter_Demo")
                return;

            if (Object.FindFirstObjectByType<DCRGameManager>() != null)
                return;

            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
                sceneCamera = Object.FindFirstObjectByType<Camera>();

            if (sceneCamera == null)
            {
                Debug.LogError("Data Center Rescue: no se encontró una cámara en la escena.");
                return;
            }

            GameObject systems = new GameObject("DCR_RuntimeSystems");
            DCRGameManager manager = systems.AddComponent<DCRGameManager>();
            manager.Initialize(sceneCamera);
        }
    }
}
