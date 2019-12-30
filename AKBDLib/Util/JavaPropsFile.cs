using AKBDLib.Exceptions;
using AKBDLib.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AKBDLib.Util
{
    public class JavaProp
    {
        public readonly string Key;

        public readonly string Value;

        public JavaProp(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public class JavaPropsFile
    {
        private readonly string _path;

        private string _content;


        public JavaPropsFile(string path)
        {
            _path = path;
            if (!File.Exists(path))
            {
                throw new ConfigurationException($"Java props file {path} not found");
            }
            _content = File.ReadAllText(path);
        }

        public void AddOrUpdateProps(IEnumerable<JavaProp> props)
        {
            GenLog.Info($"Adding/Updating properties in {_path}");
            var originalContent = _content;
            foreach (var pair in props)
            {
                AddOrUpdateProperty(pair.Key, pair.Value);
            }

            if (_content == originalContent)
            {
                GenLog.Info("No changes needed");
                return;
            }
            GenLog.Info($"Updating {_path} on disk");
            File.WriteAllText(_path, _content);
        }

        private void AddOrUpdateProperty(string key, string value)
        {
            value = value.Replace(":", @"\:");
            GenLog.Info($"Settings property {key} => {value}");

            if (Regex.IsMatch(_content, $@"{key}\s*="))
            {
                GenLog.Info("Updating existing key");

                _content = Regex.Replace(
                    _content,
                    $@"{key}\s*=.*$",
                    $"{key}={value}",
                    RegexOptions.Multiline);

                return;
            }

            GenLog.Info("Adding new property");
            _content = $"{_content}\n{key}={value}";
        }

    }
}