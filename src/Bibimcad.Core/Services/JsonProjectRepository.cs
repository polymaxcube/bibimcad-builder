using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Bibimcad.Models;

namespace Bibimcad.Services
{
    public class JsonProjectRepository
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None
        };

        private readonly string _filePath;

        public JsonProjectRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Project state file path is required.", "filePath");

            _filePath = filePath;
        }

        public ProjectState Load()
        {
            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException(
                    "Project state file was not found. Bibimcad does not create a substitute file. Expected path: " + _filePath,
                    _filePath);
            }

            string json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Project state file is empty: " + _filePath);

            ProjectState state;
            try
            {
                state = JsonConvert.DeserializeObject<ProjectState>(json, SerializerSettings);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Project state JSON is invalid: " + _filePath, ex);
            }

            if (state == null)
                throw new InvalidDataException("Project state JSON did not contain an object: " + _filePath);

            return state;
        }

        public void Save(ProjectState state)
        {
            if (state == null)
                throw new ArgumentNullException("state");

            string directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Project state path has no directory: " + _filePath);

            Directory.CreateDirectory(directory);

            string json;
            try
            {
                json = JsonConvert.SerializeObject(state, SerializerSettings);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Project state could not be serialized: " + _filePath, ex);
            }

            string tempPath = _filePath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json, Utf8NoBom);
                if (File.Exists(_filePath))
                    File.Replace(tempPath, _filePath, null);
                else
                    File.Move(tempPath, _filePath);
            }
            catch (IOException ex)
            {
                throw new IOException("Could not write project state: " + _filePath, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("Could not write project state: " + _filePath, ex);
            }
        }
    }
}
