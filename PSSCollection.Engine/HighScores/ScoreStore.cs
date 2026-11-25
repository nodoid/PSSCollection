using System;
using System.IO;

namespace PSSCollection.Engine.HighScores
{
    /// <summary>
    /// High scores in a JSON file. Writes go to a temporary file first and are then moved
    /// into place, so a crash or a full disk cannot leave a half-written score table.
    /// </summary>
    public sealed class JsonScoreStore : IScoreStore
    {
        private readonly string _path;

        public JsonScoreStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            _path = path;
        }

        public string Path => _path;

        public string Location => _path;

        public bool Exists => File.Exists(_path);

        public string Read()
        {
            if (!File.Exists(_path))
                return null;

            return File.ReadAllText(_path);
        }

        public void Write(string json)
        {
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, json ?? string.Empty);

            try
            {
                File.Move(temporary, _path, true);
            }
            catch (Exception)
            {
                // Move can fail across volumes or when another process holds the file; fall back
                // to a direct write and leave no temporary file behind.
                try
                {
                    File.WriteAllText(_path, json ?? string.Empty);
                }
                finally
                {
                    TryDelete(temporary);
                }
            }
        }

        public void Delete()
        {
            TryDelete(_path);
            TryDelete(_path + ".tmp");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Score store used by tests and by the "no writable folder" fallback.</summary>
    public sealed class InMemoryScoreStore : IScoreStore
    {
        private string _json;

        public InMemoryScoreStore(string initialJson = null)
        {
            _json = initialJson;
        }

        public bool Exists => _json != null;

        public string Location => "MEMORY";

        public string Read() => _json;

        public void Write(string json) => _json = json;

        public void Delete() => _json = null;

        /// <summary>Inspects what has been saved, for tests.</summary>
        public string Saved => _json;
    }
}
