using System.Text.RegularExpressions;

namespace LessonC03.Services;

// Minimal .env support (the Node app used `--env-file-if-exists=.env`).
// Real environment variables win over values in the file.
public static class DotEnv
{
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    // Replace `KEY=...` (or a commented `#KEY=...`) in place, or append it.
    public static void Upsert(string path, string key, string value)
    {
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        var line = $"{key}={value}";
        var re = new Regex($@"^#?\s*{Regex.Escape(key)}=.*$", RegexOptions.Multiline);
        text = re.IsMatch(text) ? re.Replace(text, line, 1) : $"{text.TrimEnd()}\n{line}\n";
        File.WriteAllText(path, text);
        Environment.SetEnvironmentVariable(key, value);
    }
}
