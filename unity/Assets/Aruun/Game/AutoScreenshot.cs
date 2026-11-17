using System.Collections;
using System.IO;
using UnityEngine;

namespace Aruun.Game
{
    /// <summary>
    /// Waits, saves a screenshot and quits. Started by --shot=path so a terminal (or a CI job)
    /// can check what the game looks like. It waits for chunks to finish building first.
    /// </summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        string _path;
        float _after;
        WorldView _view;

        public void Init(string path, float after, WorldView view)
        {
            _path = Path.GetFullPath(path);
            _after = after;
            _view = view;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float start = Time.realtimeSinceStartup;
            // At least the requested time, and until the view has nothing left to build (at most 3x).
            while (Time.realtimeSinceStartup - start < _after ||
                   (_view != null && _view.PendingChunks > 0 && Time.realtimeSinceStartup - start < _after * 3))
                yield return null;
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            ScreenCapture.CaptureScreenshot(_path);
            yield return null;
            yield return null;
            Debug.Log("Aruun screenshot saved: " + _path + " (" + _view.ShownChunks + " chunks shown)");
            Application.Quit();
        }
    }
}
