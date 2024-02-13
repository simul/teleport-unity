using UnityEditor;
using System.IO;
using UnityEditor.Build;
using UnityEngine;
using UnityEditor.Build.Reporting;

namespace teleport
{ 
    class PostprocessBuild : IPostprocessBuildWithReport
    {
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }
        public int callbackOrder { get { return 0; } }
        public void OnPostprocessBuild(BuildReport report)
        {
            TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
            string sourceCachePath = teleportSettings.cachePath;
            string targetPath= Directory.GetParent(report.summary.outputPath).ToString();
            string outputCachePath = targetPath+"/teleport_cache";
            Directory.Delete(outputCachePath,true);
            Debug.Log("teleport.PostprocessBuild, copying teleport_cache for target " + report.summary.platform + " to path " + outputCachePath);
            CopyFilesRecursively(sourceCachePath, outputCachePath);
    
        }
    }
}