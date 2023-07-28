namespace VideoStream.Data
{
    internal class VideoList
    {
        private static Dictionary<string, string> paths = new Dictionary<string, string>();

        internal static void Add(string fileName) => paths.Add(Path.GetFileName(fileName), fileName);

        internal static bool Contains(string fileName) => paths.ContainsKey(fileName);

        internal static string Get(string filename) => paths[filename];
    }
}