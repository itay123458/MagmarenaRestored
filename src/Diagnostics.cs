using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmarenaPrivate
{
    internal static class Diagnostics
    {
        public static void Write(ArenaBinding binding)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Private Magmarena diagnostics " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Arena zone: " + ZoneManagement.IsInZone(GTZone.arena));
            text.AppendLine("Forest zone: " + ZoneManagement.IsInZone(GTZone.forest));
            binding.Describe(text);
            RaycastHit[] doorwayHits = Physics.RaycastAll(new Vector3(-47.8f, 6.5f, -35.42f), Vector3.forward, 20f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(doorwayHits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
            for (int i = 0; i < Math.Min(doorwayHits.Length, 6); i++) text.AppendLine("Doorway probe " + doorwayHits[i].distance.ToString("F2") + ": " + Path(doorwayHits[i].collider.transform));
            for (int i = 0; i < SceneManager.sceneCount; i++) text.AppendLine("Scene: " + SceneManager.GetSceneAt(i).name);
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 position = camera.transform.position;
                Vector3 forward = camera.transform.forward;
                text.AppendLine("Camera: " + Path(camera.transform) + " pos=" + position + " forward=" + forward);
                RaycastHit[] hits = Physics.RaycastAll(position, forward, 30f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
                for (int i = 0; i < Math.Min(hits.Length, 16); i++)
                    text.AppendLine("Ray " + hits[i].distance.ToString("F2") + ": " + Path(hits[i].collider.transform) + " bounds=" + hits[i].collider.bounds);
                Collider[] nearby = Physics.OverlapSphere(position, 5f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < Math.Min(nearby.Length, 45); i++)
                    text.AppendLine("Nearby: " + Path(nearby[i].transform) + " bounds=" + nearby[i].bounds);
            }
            foreach (Camera active in Camera.allCameras)
                text.AppendLine("Active camera: " + Path(active.transform) + " pos=" + active.transform.position + " forward=" + active.transform.forward);
            string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagmarenaPrivate");
            Directory.CreateDirectory(folder);
            File.WriteAllText(System.IO.Path.Combine(folder, "diagnostics.txt"), text.ToString());
        }

        public static string Path(Transform target)
        {
            string path = target.name;
            while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }
    }
}
